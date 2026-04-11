using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class ReturnService
    {
        public (bool Success, string Message, Return? Return) ProcessReturn(
            int saleId, 
            int customerId, 
            List<(int ProductId, int Qty, decimal UnitPrice)> returnedItems, 
            List<(int ProductId, int Qty, decimal UnitPrice)> replacedItems,
            decimal refundPaid, 
            decimal extraReceived,
            decimal salesAdjustment,
            string returnType, 
            string? notes)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                var sale = db.Sales
                    .Include(s => s.Items)
                    .FirstOrDefault(s => s.Id == saleId);

                if (sale == null)
                    return (false, "Sale not found.", null);

                var existingReturns = db.Returns.Include(r => r.Items).Where(r => r.SaleId == saleId).ToList();
                var customer = db.Customers.Find(customerId);

                var ret = new Return
                {
                    SaleId = saleId,
                    CustomerId = customerId,
                    Date = DateTime.Now,
                    Notes = notes,
                    ReturnType = returnType,
                    Items = new List<ReturnItem>(),
                    ReplacementItems = new List<ReplacementItem>()
                };

                decimal returnedTotal = 0;
                foreach (var item in returnedItems)
                {
                    var saleItem = sale.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
                    if (saleItem == null) return (false, $"Product ID {item.ProductId} was not part of this sale.", null);

                    int previouslyReturned = existingReturns.SelectMany(r => r.Items).Where(i => i.ProductId == item.ProductId).Sum(i => i.Quantity);
                    if (item.Qty + previouslyReturned > saleItem.Quantity)
                        return (false, $"Return quantity for {item.ProductId} exceeds sold quantity.", null);

                    decimal lineTotal = item.Qty * item.UnitPrice;
                    returnedTotal += lineTotal;

                    ret.Items.Add(new ReturnItem { ProductId = item.ProductId, Quantity = item.Qty, UnitPrice = item.UnitPrice, Total = lineTotal });

                    var product = db.Products.Find(item.ProductId);
                    if (product != null)
                    {
                        product.Quantity   += item.Qty;
                        product.StockUnits  = product.Quantity; // keep in sync
                    }
                }

                decimal replacementTotal = 0;
                decimal replacementProfit = 0;
                foreach (var item in replacedItems)
                {
                    var product = db.Products.Find(item.ProductId);
                    if (product == null) return (false, $"Replacement product ID {item.ProductId} not found.", null);
                    int availStock = product.StockUnits > 0 ? product.StockUnits : product.Quantity;
                    if (availStock < item.Qty) return (false, $"Insufficient stock for replacement product {product.Name}.", null);

                    decimal lineTotal = item.Qty * item.UnitPrice;
                    replacementTotal += lineTotal;

                    decimal itemPurchasePrice = product.UnitPrice > 0 ? product.UnitPrice : product.PurchasePrice;
                    replacementProfit += (item.UnitPrice - itemPurchasePrice) * item.Qty;

                    ret.ReplacementItems.Add(new ReplacementItem { ProductId = item.ProductId, Quantity = item.Qty, UnitPrice = item.UnitPrice, Total = lineTotal });
                    product.Quantity  -= item.Qty;
                    product.StockUnits = product.Quantity; // keep in sync
                }

                ret.TotalAmount = returnedTotal;
                ret.ReplaceAmount = replacementTotal;
                ret.RefundAmount = refundPaid; // Amount actually given to customer

                // Net difference logic
                // If replacementTotal > returnedTotal, customer should pay (extraReceived)
                // If replacementTotal < returnedTotal, store should refund (refundPaid)
                
                // Update customer balance if it's not a walk-in or even if it is, 
                // but usually balance is for credit customers.
                if (customer != null && !customer.IsWalkIn)
                {
                    // The net financial impact on the customer's balance.
                    // Positive value means customer owes more, negative means customer owes less (or store owes customer).
                    // (replacementTotal - returnedTotal) is the net value of items exchanged.
                    // If positive, customer owes this amount. If negative, store owes this amount.
                    // extraReceived is cash paid by customer, reduces what they owe.
                    // refundPaid is cash given to customer, increases what store owes (or reduces what customer owes).
                    decimal netItemValueDifference = replacementTotal - returnedTotal;
                    decimal netCashTransaction = extraReceived - refundPaid; // Positive if customer paid, negative if customer received.
                    
                    // The customer's balance should reflect the net item value difference minus any cash transaction.
                    // If netItemValueDifference is positive (customer owes), and customer paid extraReceived, their balance decreases by extraReceived.
                    // If netItemValueDifference is negative (store owes), and store paid refundPaid, their balance increases by refundPaid (meaning store owes less).
                    customer.Balance += netItemValueDifference - netCashTransaction;
                }

                // Recalculate Sale Profit
                decimal updatedTotalProfit = 0;
                foreach (var sItem in sale.Items)
                {
                    int alreadyReturnedQty = existingReturns.SelectMany(r => r.Items).Where(i => i.ProductId == sItem.ProductId).Sum(i => i.Quantity);
                    int currentReturnQty = returnedItems.Where(i => i.ProductId == sItem.ProductId).Sum(i => i.Qty);
                    int totalReturned = alreadyReturnedQty + currentReturnQty;
                    int finalSoldQty = sItem.Quantity - totalReturned;

                    if (finalSoldQty > 0)
                    {
                        decimal proportionalDiscount = sale.SubTotal > 0 ? (sItem.Total / sale.SubTotal) * sale.Discount : 0;
                        decimal finalSellingPrice = sItem.Quantity > 0 ? (sItem.Total - proportionalDiscount) / sItem.Quantity : 0;
                        updatedTotalProfit += (finalSellingPrice - sItem.PurchasePrice) * finalSoldQty;
                    }
                }
                
                sale.SubTotal += (replacementTotal - returnedTotal);
                sale.NetAmount += (replacementTotal - returnedTotal) + salesAdjustment;
                sale.PaidAmount += (extraReceived - refundPaid);
                sale.Profit = updatedTotalProfit + replacementProfit + salesAdjustment;
                db.Sales.Update(sale);

                db.Returns.Add(ret);
                db.SaveChanges();
                transaction.Commit();

                var saved = db.Returns
                    .Include(r => r.Items).ThenInclude(i => i.Product)
                    .Include(r => r.ReplacementItems).ThenInclude(i => i.Product)
                    .Include(r => r.Customer)
                    .First(r => r.Id == ret.Id);

                return (true, "Return/Replacement processed successfully.", saved);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Error processing return: {ex.Message}", null);
            }
        }

        public List<ReturnItem> GetRecentReturnItems(int count = 50)
        {
            using var db = new AppDbContext();
            return db.ReturnItems
                .Include(i => i.Product)
                .Include(i => i.Return).ThenInclude(r => r.Customer)
                .OrderByDescending(i => i.Return!.Date)
                .Take(count)
                .ToList();
        }

        public List<Return> GetAll()
        {
            using var db = new AppDbContext();

            return db.Returns
                .Include(r => r.Sale)
                .Include(r => r.Customer)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                .OrderByDescending(r => r.Date)
                .AsEnumerable()
                .ToList();
        }

        public (bool Success, string Message) ProcessManualRefund(Return model, decimal discountDeducted, List<(int ProductId, int Qty, decimal UnitPrice)> replacedItems)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();
            
            try 
            {
                var dummySale = db.Sales.FirstOrDefault(s => s.InvoiceNo == "MANUAL-REFUND");
                if (dummySale == null)
                {
                    dummySale = new Sale { InvoiceNo = "MANUAL-REFUND", CustomerId = 1, UserId = 1, Date = DateTime.Now };
                    db.Sales.Add(dummySale);
                    db.SaveChanges();
                }

                model.SaleId = dummySale.Id;
                
                db.Returns.Add(model);

                decimal returnedTotal = 0;
                decimal returnedProfit = 0;

                foreach (var item in model.Items)
                {
                    var product = db.Products.Find(item.ProductId);
                    if (product != null)
                    {
                        product.Quantity   += item.Quantity; // Increase stock
                        product.StockUnits  = product.Quantity; // keep in sync
                        returnedTotal += item.Total;
                        returnedProfit += (item.UnitPrice - product.PurchasePrice) * item.Quantity;
                    }
                }

                dummySale.SubTotal -= returnedTotal;
                dummySale.NetAmount -= returnedTotal;
                dummySale.PaidAmount -= returnedTotal;
                dummySale.Profit -= returnedProfit;
                db.Sales.Update(dummySale);

                decimal replacementTotal = 0;
                decimal replacementProfit = 0;

                if (replacedItems != null && replacedItems.Any())
                {
                    var replacementSale = new Sale
                    {
                        InvoiceNo = $"REP-{DateTime.Now:yyMMddHHmmss}",
                        CustomerId = 1, // Default Walk-in
                        UserId = 1,     // Fallback to default Admin/User ID
                        Date = DateTime.Now
                    };

                    foreach (var repl in replacedItems)
                    {
                        var product = db.Products.Find(repl.ProductId);
                        decimal itemPurchasePrice = 0;

                        if (product != null)
                        {
                            int availRepl = product.StockUnits > 0 ? product.StockUnits : product.Quantity;
                            if (availRepl < repl.Qty)
                                throw new Exception($"Insufficient stock for replacement product {product.Name}");

                            product.Quantity   -= repl.Qty;
                            product.StockUnits  = product.Quantity;
                            itemPurchasePrice = product.PurchasePrice;
                        }

                        decimal lineTotal = repl.Qty * repl.UnitPrice;
                        replacementTotal += lineTotal;
                        replacementProfit += (repl.UnitPrice - itemPurchasePrice) * repl.Qty;

                        replacementSale.Items.Add(new SaleItem
                        {
                            ProductId = repl.ProductId,
                            Quantity = repl.Qty,
                            UnitPrice = repl.UnitPrice,
                            PurchasePrice = itemPurchasePrice,
                            Total = lineTotal
                        });
                    }

                    replacementSale.SubTotal = replacementTotal;
                    replacementSale.NetAmount = replacementTotal;
                    replacementSale.PaidAmount = replacementTotal;
                    replacementSale.Profit = replacementProfit;

                    db.Sales.Add(replacementSale);
                }

                model.ReplaceAmount = replacementTotal;

                db.SaveChanges();
                transaction.Commit();
                return (true, "Manual refund processed successfully.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Failed to process manual refund: {ex.Message}");
            }
        }

        public List<Return> GetByDateRange(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();

            return db.Returns
                .Include(r => r.Sale)
                .Include(r => r.Customer)
                .Include(r => r.Items)
                    .ThenInclude(i => i.Product)
                .Where(r => r.Date >= from && r.Date <= to)
                .OrderByDescending(r => r.Date)
                .AsEnumerable()
                .ToList();
        }

        public decimal GetRefundTotal(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();

            return db.Returns
                .Where(r => r.Date >= from && r.Date <= to)
                .Select(r => r.RefundAmount)
                .AsEnumerable()
                .Sum();
        }

        public (bool Success, string Message, Return? Return) ProcessDirectReturn(
            int customerId, 
            List<(int ProductId, int Qty, decimal UnitPrice)> returnedItems, 
            List<(int ProductId, int Qty, decimal UnitPrice)> replacedItems,
            decimal refundPaid,
            decimal extraReceived,
            decimal salesAdjustment,
            string returnType,
            string? notes = null,
            string? originalInvoiceNo = null)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();
            
            try 
            {
                var customer = db.Customers.Find(customerId);

                Sale? linkedSale = null;

                if (!string.IsNullOrWhiteSpace(originalInvoiceNo))
                {
                    linkedSale = db.Sales.FirstOrDefault(s => s.InvoiceNo == originalInvoiceNo.Trim());
                }

                if (linkedSale == null)
                {
                    // Fetch the most recent sale for this customer or the first sale in DB as a placeholder for SaleId
                    linkedSale = db.Sales.OrderByDescending(s => s.Id).FirstOrDefault(s => s.CustomerId == customerId) 
                                    ?? db.Sales.FirstOrDefault();
                }

                if (linkedSale == null)
                {
                    linkedSale = new Sale { InvoiceNo = $"DUMMY-{DateTime.Now.Ticks}", CustomerId = customerId, UserId = 1, Date = DateTime.Now };
                    db.Sales.Add(linkedSale);
                    db.SaveChanges();
                }

                var ret = new Return
                {
                    SaleId = linkedSale.Id,
                    CustomerId = customerId,
                    Date = DateTime.Now,
                    Notes = string.IsNullOrWhiteSpace(notes) ? $"Direct POS {returnType}" : notes,
                    ReturnType = returnType,
                    Items = new List<ReturnItem>(),
                    ReplacementItems = new List<ReplacementItem>()
                };

                decimal returnedTotal = 0;
                decimal returnedProfit = 0;
                foreach (var item in returnedItems)
                {
                    decimal lineTotal = item.Qty * item.UnitPrice;
                    returnedTotal += lineTotal;

                    ret.Items.Add(new ReturnItem { ProductId = item.ProductId, Quantity = item.Qty, UnitPrice = item.UnitPrice, Total = lineTotal });

                    var product = db.Products.Find(item.ProductId);
                    if (product != null)
                    {
                        product.Quantity   += item.Qty;
                        product.StockUnits  = product.Quantity; // keep in sync
                        returnedProfit += (item.UnitPrice - product.PurchasePrice) * item.Qty;
                    }
                }

                if (linkedSale != null)
                {
                    linkedSale.SubTotal -= returnedTotal;
                    linkedSale.NetAmount += (-returnedTotal + salesAdjustment);
                    linkedSale.PaidAmount -= returnedTotal;
                    linkedSale.Profit += (-returnedProfit + salesAdjustment);
                    db.Sales.Update(linkedSale);
                }

                decimal replacementTotal = 0;
                decimal replacementProfit = 0;
                Sale? replacementSale = null;

                if (replacedItems.Any())
                {
                    replacementSale = new Sale
                    {
                        InvoiceNo = $"REP-{DateTime.Now:yyMMddHHmmss}",
                        CustomerId = customerId,
                        UserId = 1,
                        Date = DateTime.Now
                    };
                }

                foreach (var item in replacedItems)
                {
                    var product = db.Products.Find(item.ProductId);
                    if (product == null) return (false, $"Replacement product ID {item.ProductId} not found.", null);
                    int availDirect = product.StockUnits > 0 ? product.StockUnits : product.Quantity;
                    if (availDirect < item.Qty) return (false, $"Insufficient stock for {product.Name}.", null);

                    decimal lineTotal = item.Qty * item.UnitPrice;
                    replacementTotal += lineTotal;
                    replacementProfit += (item.UnitPrice - product.PurchasePrice) * item.Qty;

                    ret.ReplacementItems.Add(new ReplacementItem { ProductId = item.ProductId, Quantity = item.Qty, UnitPrice = item.UnitPrice, Total = lineTotal });
                    product.Quantity  -= item.Qty;
                    product.StockUnits = product.Quantity; // keep in sync

                    if (replacementSale != null)
                    {
                        replacementSale.Items.Add(new SaleItem
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Qty,
                            UnitPrice = item.UnitPrice,
                            PurchasePrice = product.PurchasePrice,
                            Total = lineTotal
                        });
                    }
                }

                if (replacementSale != null)
                {
                    replacementSale.SubTotal = replacementTotal;
                    replacementSale.NetAmount = replacementTotal;
                    replacementSale.PaidAmount = replacementTotal;
                    replacementSale.Profit = replacementProfit;
                    db.Sales.Add(replacementSale);
                }

                ret.TotalAmount = returnedTotal;
                ret.ReplaceAmount = replacementTotal;
                ret.RefundAmount = refundPaid;

                if (customer != null && !customer.IsWalkIn)
                {
                    // The net financial impact on the customer's balance.
                    // Positive value means customer owes more, negative means customer owes less (or store owes customer).
                    // (replacementTotal - returnedTotal) is the net value of items exchanged.
                    // If positive, customer owes this amount. If negative, store owes this amount.
                    // extraReceived is cash paid by customer, reduces what they owe.
                    // refundPaid is cash given to customer, increases what store owes (or reduces what customer owes).
                    decimal netItemValueDifference = replacementTotal - returnedTotal;
                    decimal netCashTransaction = extraReceived - refundPaid; // Positive if customer paid, negative if customer received.
                    
                    // The customer's balance should reflect the net item value difference minus any cash transaction.
                    // If netItemValueDifference is positive (customer owes), and customer paid extraReceived, their balance decreases by extraReceived.
                    // If netItemValueDifference is negative (store owes), and store paid refundPaid, their balance increases by refundPaid (meaning store owes less).
                    customer.Balance += netItemValueDifference - netCashTransaction;
                }

                db.Returns.Add(ret);
                db.SaveChanges();
                transaction.Commit();

                var saved = db.Returns
                    .Include(r => r.Items).ThenInclude(i => i.Product)
                    .Include(r => r.ReplacementItems).ThenInclude(i => i.Product)
                    .Include(r => r.Customer)
                    .First(r => r.Id == ret.Id);

                return (true, $"{returnType} processed successfully.", saved);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Error: {ex.Message}", null);
            }
        }
    }
}