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
                        ProductId    = item.ProductId,
                        Quantity     = totalUnits,       // legacy: total units for stock compat
                        UnitPrice    = unitPrice,        // per-unit price for reports
                        Total        = lineTotal,
                        // Pack-Unit fields
                        PackQty      = packQty,
                        UnitsPerPack = unitsPerPack,
                        PackPrice    = packPrice,
                        TotalUnits   = totalUnits
                    });

                    // ── Update product stock ──────────────────────────────────────
                    var product = db.Products.Find(item.ProductId);
                    if (product != null)
                    {
                        // Always add totalUnits to stock (works for both pack and direct mode)
                        product.StockUnits += totalUnits;
                        product.Quantity    = product.StockUnits; // keep in sync

                        // Update pack metadata
                        if (unitsPerPack > 1)
                            product.UnitsPerPack = unitsPerPack;

                        // Update purchase price at unit level
                        decimal prevUnitPurchase = product.PurchasePrice;
                        product.PurchasePrice = unitPrice;

                        if (packPrice > 0)
                            product.PackPrice = packPrice;

                        // Dynamic Sale Price Adjustment
                        if (unitPrice > prevUnitPurchase && prevUnitPurchase > 0)
                        {
                            // Preserve existing profit margin
                            decimal margin = (product.SalePrice > 0 ? product.SalePrice : product.UnitPrice) - prevUnitPurchase;
                            decimal newSalePrice = unitPrice + margin;
                            product.SalePrice = newSalePrice;
                            product.UnitPrice = newSalePrice;
                            if (unitsPerPack > 1)
                                product.PackPrice = newSalePrice * unitsPerPack;
                        }
                        else if (prevUnitPurchase == 0)
                        {
                            // First purchase – initialize unit price
                            if (product.UnitPrice == 0) product.UnitPrice = unitPrice;
                            if (product.SalePrice == 0) product.SalePrice = unitPrice;
                        }
                    }
                }

                purchase.TotalAmount = total;
                purchase.PaidAmount  = paidAmount;
                purchase.DueAmount   = total - paidAmount;

                // Increase supplier balance by the unpaid amount
                supplier.Balance += purchase.DueAmount;

                db.Purchases.Add(purchase);
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
