using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class PurchaseService
    {
        /// <summary>
        /// Creates a purchase record.
        /// When EnablePackUnitSelling is ON, each item carries PackQty, PackPrice, and UnitsPerPack.
        /// When OFF (legacy mode), Qty = units, UnitPrice = per-unit price.
        /// </summary>
        public (bool Success, string Message) CreatePurchase(
            int supplierId,
            List<(int ProductId, int PackQty, decimal PackPrice, int UnitsPerPack)> items,
            decimal paidAmount,
            string? notes)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                var supplier = db.Suppliers.Find(supplierId);
                if (supplier == null) return (false, "Supplier not found.");

                var purchase = new Purchase
                {
                    SupplierId = supplierId,
                    Date = DateTime.Now,
                    Notes = notes
                };

                decimal total = 0;

                foreach (var item in items)
                {
                    // ── Null-safe defaults ─────────────────────────────────────────
                    int unitsPerPack = item.UnitsPerPack > 0 ? item.UnitsPerPack : 1;
                    int packQty      = item.PackQty      > 0 ? item.PackQty      : 1;
                    decimal packPrice  = item.PackPrice;

                    // Derived values
                    decimal unitPrice  = unitsPerPack > 1 && packPrice > 0
                        ? packPrice / unitsPerPack
                        : packPrice;                  // direct-unit price when UPP=1

                    int     totalUnits = packQty * unitsPerPack;
                    decimal lineTotal  = packQty * packPrice;

                    // In pack mode the line total uses pack price; in unit mode use unitPrice×qty
                    if (unitsPerPack == 1) lineTotal = totalUnits * unitPrice;

                    total += lineTotal;

                    purchase.Items.Add(new PurchaseItem
                    {
                        ProductId     = item.ProductId,
                        Quantity      = totalUnits,       // legacy: total units for stock compat
                        UnitPrice     = unitPrice,        // per-unit price for reports
                        Total         = lineTotal,
                        // Pack-Unit fields
                        PackQty       = packQty,
                        UnitsPerPack  = unitsPerPack,
                        PackPrice     = packPrice,
                        TotalUnits    = totalUnits,
                        ReceivedUnits = totalUnits        // immediate purchase = fully received
                    });

                    // ── Update product stock & weighted-average cost ──────────────
                    var product = db.Products.Find(item.ProductId);
                    if (product != null)
                    {
                        int     prevUnits = product.StockUnits > 0 ? product.StockUnits : product.Quantity;
                        decimal prevCost  = product.PurchasePrice;
                        int     newTotalUnits = prevUnits + totalUnits;

                        // Weighted-average cost:
                        //   newCost = (prevUnits*prevCost + addedUnits*addedCost) / totalUnits
                        // Falls back to the incoming unit cost for first stock or when no prior
                        // cost exists, so the cost basis used for profit is always meaningful.
                        if (newTotalUnits > 0 && prevUnits > 0 && prevCost > 0)
                            product.PurchasePrice = decimal.Round(
                                (prevUnits * prevCost + totalUnits * unitPrice) / newTotalUnits, 2);
                        else
                            product.PurchasePrice = unitPrice;

                        product.StockUnits = newTotalUnits;
                        product.Quantity   = product.StockUnits; // keep in sync

                        // Update pack metadata (UnitsPerPack only – PackPrice is a SELLING price
                        // owned by the product/pricing screen, never set from purchase cost here).
                        if (unitsPerPack > 1)
                            product.UnitsPerPack = unitsPerPack;

                        // Initialise the SELLING price on first purchase only. We never silently
                        // overwrite a price the user has set — explicit pricing avoids surprises.
                        if (product.SalePrice == 0)
                        {
                            product.SalePrice = unitPrice;
                            product.UnitPrice = unitPrice;
                        }
                    }
                }

                purchase.TotalAmount = total;
                purchase.PaidAmount  = paidAmount;
                purchase.DueAmount   = total - paidAmount;
                purchase.Status      = "Received"; // quick purchase is immediately received

                // Increase supplier balance by the unpaid amount
                supplier.Balance += purchase.DueAmount;

                db.Purchases.Add(purchase);

                // Record any amount paid at purchase time as a SupplierPayment history row.
                // Balance is already correct (only the unpaid due was added above); this row
                // exists purely so the supplier ledger/history reconciles with the balance.
                if (paidAmount > 0)
                {
                    db.SupplierPayments.Add(new SupplierPayment
                    {
                        SupplierId    = supplierId,
                        Amount        = paidAmount,
                        Date          = purchase.Date,
                        PaymentMethod = "Cash",
                        Notes         = "Paid at purchase"
                    });
                }

                db.SaveChanges();
                transaction.Commit();

                return (true, $"Purchase recorded. Total: {total:N2}");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Error recording purchase: {ex.Message}");
            }
        }

        /// <summary>Legacy overload — keeps old call sites (qty = units, unitPrice = per-unit price) working.</summary>
        public (bool Success, string Message) CreatePurchase(
            int supplierId,
            List<(int ProductId, int Qty, decimal UnitPrice)> items,
            decimal paidAmount,
            string? notes)
        {
            // Convert to new signature: treat as 1 unit per pack
            var converted = items.Select(i => (i.ProductId, i.Qty, i.UnitPrice, 1)).ToList();
            return CreatePurchase(supplierId, converted, paidAmount, notes);
        }

        /// <summary>
        /// Cancels/deletes a purchase and reverses its effects:
        ///  • removes the received units from stock (clamped at 0),
        ///  • removes the still-unpaid amount from the supplier's payable,
        ///  • deletes the purchase, its items and the "Paid at purchase" history row.
        /// Note: the weighted-average cost is not un-rolled (previous cost is not restorable),
        /// so re-check the product's cost if it was averaged from this purchase.
        /// </summary>
        public (bool Success, string Message) CancelPurchase(int purchaseId)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var purchase = db.Purchases
                    .Include(p => p.Items)
                    .FirstOrDefault(p => p.Id == purchaseId);
                if (purchase == null) return (false, "Purchase not found.");

                var supplier = db.Suppliers.Find(purchase.SupplierId);

                // Reverse only the units that were actually RECEIVED into stock.
                // (An "Ordered" PO that was never received has ReceivedUnits = 0 and
                //  did not affect stock or the supplier balance, so nothing is reversed.)
                foreach (var it in purchase.Items)
                {
                    var product = db.Products.Find(it.ProductId);
                    if (product != null)
                    {
                        int units = it.ReceivedUnits > 0 ? it.ReceivedUnits
                                    : (it.TotalUnits > 0 ? it.TotalUnits : it.Quantity);
                        int cur   = product.StockUnits > 0 ? product.StockUnits : product.Quantity;
                        product.StockUnits = Math.Max(0, cur - units);
                        product.Quantity   = product.StockUnits;
                    }
                }

                // Reverse the remaining payable this purchase contributed
                if (supplier != null)
                    supplier.Balance -= purchase.DueAmount;

                // Remove the "Paid at purchase" history row (if any) created for this purchase
                if (purchase.PaidAmount > 0)
                {
                    var payRow = db.SupplierPayments
                        .Where(sp => sp.SupplierId == purchase.SupplierId
                                     && sp.Amount == purchase.PaidAmount
                                     && sp.Notes == "Paid at purchase"
                                     && sp.Date == purchase.Date)
                        .FirstOrDefault();
                    if (payRow != null) db.SupplierPayments.Remove(payRow);
                }

                db.PurchaseItems.RemoveRange(purchase.Items);
                db.Purchases.Remove(purchase);

                db.SaveChanges();
                transaction.Commit();
                AuditService.Log("Purchase Cancelled", "Purchase", purchaseId,
                    $"Reversed purchase of {purchase.TotalAmount:N2} from supplier #{purchase.SupplierId}");
                return (true, "Purchase cancelled and stock/supplier balance reversed.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Error cancelling purchase: {ex.Message}");
            }
        }

        // ── Purchase-Order lifecycle ─────────────────────────────────────────────

        private static decimal WeightedAvgCost(int prevUnits, decimal prevCost, int addUnits, decimal addCost)
        {
            int total = prevUnits + addUnits;
            if (total > 0 && prevUnits > 0 && prevCost > 0)
                return decimal.Round((prevUnits * prevCost + addUnits * addCost) / total, 2);
            return addCost;
        }

        private static (PurchaseItem item, decimal lineTotal) BuildOrderedItem(
            (int ProductId, int PackQty, decimal PackPrice, int UnitsPerPack) it)
        {
            int unitsPerPack = it.UnitsPerPack > 0 ? it.UnitsPerPack : 1;
            int packQty      = it.PackQty      > 0 ? it.PackQty      : 1;
            decimal packPrice = it.PackPrice;
            decimal unitPrice = unitsPerPack > 1 && packPrice > 0 ? packPrice / unitsPerPack : packPrice;
            int totalUnits    = packQty * unitsPerPack;
            decimal lineTotal = unitsPerPack == 1 ? totalUnits * unitPrice : packQty * packPrice;

            var pi = new PurchaseItem
            {
                ProductId     = it.ProductId,
                Quantity      = totalUnits,
                UnitPrice     = unitPrice,
                Total         = lineTotal,
                PackQty       = packQty,
                UnitsPerPack  = unitsPerPack,
                PackPrice     = packPrice,
                TotalUnits    = totalUnits,
                ReceivedUnits = 0
            };
            return (pi, lineTotal);
        }

        /// <summary>
        /// Creates a Purchase ORDER (status = "Ordered"). Nothing is added to stock and the
        /// supplier's payable is NOT changed until the goods are received via ReceivePurchase.
        /// </summary>
        public (bool Success, string Message, int PurchaseId) CreatePurchaseOrder(
            int supplierId,
            List<(int ProductId, int PackQty, decimal PackPrice, int UnitsPerPack)> items,
            string? notes)
        {
            using var db = new AppDbContext();
            try
            {
                var supplier = db.Suppliers.Find(supplierId);
                if (supplier == null) return (false, "Supplier not found.", 0);
                if (items == null || items.Count == 0) return (false, "No items on the order.", 0);

                var purchase = new Purchase
                {
                    SupplierId = supplierId,
                    Date       = DateTime.Now,
                    Notes      = notes,
                    Status     = "Ordered",
                    PaidAmount = 0,
                    DueAmount  = 0
                };

                decimal total = 0;
                foreach (var it in items)
                {
                    var (pi, lineTotal) = BuildOrderedItem(it);
                    total += lineTotal;
                    purchase.Items.Add(pi);
                }
                purchase.TotalAmount = total;

                db.Purchases.Add(purchase);
                db.SaveChanges();
                AuditService.Log("Purchase Order Created", "Purchase", purchase.Id,
                    $"PO for supplier #{supplierId}, total {total:N2} ({purchase.Items.Count} items)");
                return (true, $"Purchase order created. Total: {total:N2}", purchase.Id);
            }
            catch (Exception ex)
            {
                return (false, $"Error creating purchase order: {ex.Message}", 0);
            }
        }

        /// <summary>Edits an "Ordered" purchase order (not allowed once any goods are received).</summary>
        public (bool Success, string Message) UpdatePurchaseOrder(
            int purchaseId,
            List<(int ProductId, int PackQty, decimal PackPrice, int UnitsPerPack)> items,
            string? notes)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var purchase = db.Purchases.Include(p => p.Items).FirstOrDefault(p => p.Id == purchaseId);
                if (purchase == null) return (false, "Purchase order not found.");
                if (purchase.Status != "Ordered")
                    return (false, "Only orders that have not been received yet can be edited.");
                if (items == null || items.Count == 0) return (false, "No items on the order.");

                db.PurchaseItems.RemoveRange(purchase.Items);
                purchase.Items.Clear();

                decimal total = 0;
                foreach (var it in items)
                {
                    var (pi, lineTotal) = BuildOrderedItem(it);
                    pi.PurchaseId = purchase.Id;
                    total += lineTotal;
                    purchase.Items.Add(pi);
                }
                purchase.TotalAmount = total;
                purchase.Notes = notes;

                db.SaveChanges();
                transaction.Commit();
                AuditService.Log("Purchase Order Edited", "Purchase", purchase.Id, $"New total {total:N2}");
                return (true, "Purchase order updated.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Error updating purchase order: {ex.Message}");
            }
        }

        /// <summary>
        /// Receives some/all outstanding quantities of an order into stock. Adds received units
        /// (weighted-average cost) and increases the supplier's payable by the received value.
        /// Sets the order status to PartiallyReceived / Received accordingly.
        /// </summary>
        public (bool Success, string Message) ReceivePurchase(
            int purchaseId,
            List<(int PurchaseItemId, int ReceiveQty)> receipts)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var purchase = db.Purchases.Include(p => p.Items).FirstOrDefault(p => p.Id == purchaseId);
                if (purchase == null) return (false, "Purchase order not found.");
                if (purchase.Status == "Cancelled") return (false, "This order was cancelled.");
                if (purchase.Status == "Received") return (false, "This order is already fully received.");

                var supplier = db.Suppliers.Find(purchase.SupplierId);
                if (supplier == null) return (false, "Supplier not found.");

                decimal receivedValue = 0;
                foreach (var r in receipts)
                {
                    if (r.ReceiveQty <= 0) continue;
                    var item = purchase.Items.FirstOrDefault(i => i.Id == r.PurchaseItemId);
                    if (item == null) continue;

                    int remaining = item.TotalUnits - item.ReceivedUnits;
                    if (remaining <= 0) continue;
                    int recv = Math.Min(r.ReceiveQty, remaining);

                    var product = db.Products.Find(item.ProductId);
                    if (product != null)
                    {
                        int prevUnits = product.StockUnits > 0 ? product.StockUnits : product.Quantity;
                        product.PurchasePrice = WeightedAvgCost(prevUnits, product.PurchasePrice, recv, item.UnitPrice);
                        product.StockUnits = prevUnits + recv;
                        product.Quantity   = product.StockUnits;
                        if (item.UnitsPerPack > 1) product.UnitsPerPack = item.UnitsPerPack;
                        if (product.SalePrice == 0) { product.SalePrice = item.UnitPrice; product.UnitPrice = item.UnitPrice; }
                    }

                    item.ReceivedUnits += recv;
                    receivedValue += recv * item.UnitPrice;
                }

                if (receivedValue <= 0) return (false, "Nothing was received. Check the quantities.");

                // Received goods increase what we owe the supplier.
                supplier.Balance   += receivedValue;
                purchase.DueAmount += receivedValue;

                bool allReceived = purchase.Items.All(i => i.ReceivedUnits >= i.TotalUnits);
                bool anyReceived = purchase.Items.Any(i => i.ReceivedUnits > 0);
                purchase.Status = allReceived ? "Received" : (anyReceived ? "PartiallyReceived" : "Ordered");

                db.SaveChanges();
                transaction.Commit();
                AuditService.Log("Purchase Received", "Purchase", purchase.Id,
                    $"Received {receivedValue:N2} of goods. Status now {purchase.Status}.");
                return (true, $"Received goods worth {receivedValue:N2}. Status: {purchase.Status}.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Error receiving purchase: {ex.Message}");
            }
        }

        public Purchase? GetPurchaseById(int id)
        {
            using var db = new AppDbContext();
            return db.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.Items).ThenInclude(i => i.Product)
                .FirstOrDefault(p => p.Id == id);
        }

        public List<PurchaseItem> GetRecentPurchaseItems(int count = 50)
        {
            using var db = new AppDbContext();
            return db.PurchaseItems
                .Include(i => i.Product)
                .Include(i => i.Purchase).ThenInclude(p => p.Supplier)
                .OrderByDescending(i => i.Purchase!.Date)
                .Take(count)
                .ToList();
        }

        public List<Purchase> GetAll()
        {
            using var db = new AppDbContext();
            return db.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.Items).ThenInclude(i => i.Product)
                .OrderByDescending(p => p.Date)
                .ToList();
        }

        public List<Purchase> GetByDateRange(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return db.Purchases
                .Include(p => p.Supplier)
                .Include(p => p.Items).ThenInclude(i => i.Product)
                .AsEnumerable()
                .Where(p => p.Date >= from && p.Date <= to)
                .OrderByDescending(p => p.Date)
                .ToList();
        }

        public decimal GetTotalPurchases(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return db.Purchases
                .AsEnumerable()
                .Where(p => p.Date >= from && p.Date <= to)
                .Select(p => p.TotalAmount)
                .Sum();
        }
    }
}
