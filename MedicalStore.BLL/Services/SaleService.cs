using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Helpers;
using MedicalStore.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class SaleService
    {
        /// <summary>
        /// Core sale creation method with full pack+unit support.
        /// Each item: ProductId, Packs (whole packs), LooseUnits (individual units), UnitPrice.
        /// When EnablePackUnitSelling is OFF, Packs=0, LooseUnits = legacy Qty, UnitPrice = SalePrice.
        /// </summary>
        public (bool Success, string Message, Sale? Sale) CreateSale(
            int customerId,
            List<(int ProductId, int Packs, int LooseUnits, decimal UnitPrice)> items,
            decimal discount,
            decimal paidAmount,
            decimal changeAmount = 0,
            bool addExcessToCredit = false)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                // Generate invoice number
                var today = DateTime.Now;
                var count = db.Sales.Count(s => s.Date.Date == today.Date) + 1;
                var invoiceNo = $"INV-{today:yyyyMMdd}-{count:D4}";

                var sale = new Sale
                {
                    InvoiceNo = invoiceNo,
                    CustomerId = customerId,
                    UserId = AppSession.CurrentUserId,
                    Date = today,
                    Discount = discount
                };

                decimal subTotal = 0;
                decimal totalProfit = 0;

                foreach (var item in items)
                {
                    var product = db.Products.Find(item.ProductId);
                    decimal itemPurchasePrice = 0;

                    int unitsPerPack = (product?.UnitsPerPack ?? 1) > 0 ? (product?.UnitsPerPack ?? 1) : 1;
                    int totalUnits   = item.Packs * unitsPerPack + item.LooseUnits;
                    if (totalUnits <= 0) continue;

                    decimal lineTotal = totalUnits * item.UnitPrice;
                    subTotal += lineTotal;

                    if (product != null)
                    {
                        int availableUnits = product.StockUnits > 0 ? product.StockUnits : product.Quantity;

                        if (availableUnits < totalUnits)
                        {
                            string available = product.StockDisplay;
                            return (false, $"Insufficient stock for {product.Name}. Available: {available}", null);
                        }

                        // Deduct stock
                        product.StockUnits = availableUnits - totalUnits;
                        product.Quantity   = product.StockUnits;

                        itemPurchasePrice = product.UnitPrice > 0 ? product.UnitPrice : product.PurchasePrice;
                    }

                    sale.Items.Add(new SaleItem
                    {
                        ProductId     = item.ProductId,
                        Quantity      = totalUnits,          // legacy compat
                        UnitPrice     = item.UnitPrice,
                        PurchasePrice = itemPurchasePrice,
                        Total         = lineTotal,
                        // Pack-Unit fields
                        Packs         = item.Packs,
                        LooseUnits    = item.LooseUnits,
                        TotalUnits    = totalUnits,
                        UnitsPerPack  = unitsPerPack
                    });
                }

                sale.SubTotal = subTotal;

                // Calculate profit (proportional discount per item)
                foreach (var sItem in sale.Items)
                {
                    decimal proportionalDiscount = subTotal > 0 ? (sItem.Total / subTotal) * discount : 0;
                    decimal finalSellingPrice     = sItem.TotalUnits > 0 ? (sItem.Total - proportionalDiscount) / sItem.TotalUnits : 0;
                    decimal itemProfit            = (finalSellingPrice - sItem.PurchasePrice) * sItem.TotalUnits;
                    totalProfit += itemProfit;
                }

                // Calculate Tax
                decimal netAfterDiscount = subTotal - discount;
                decimal taxAmount = 0;
                if (AppConstants.TaxRate > 0)
                    taxAmount = netAfterDiscount * (AppConstants.TaxRate / 100);

                sale.Tax       = taxAmount;
                sale.NetAmount = netAfterDiscount + taxAmount;
                sale.PaidAmount = paidAmount;
                sale.Profit    = totalProfit;

                var customer = db.Customers.Find(customerId);

                if (customer != null && !customer.IsWalkIn)
                {
                    // For registered customers, the new bill increases their debt:
                    decimal initialBalance = customer.Balance;
                    decimal creditApplied = 0;

                    if (initialBalance < 0)
                    {
                        creditApplied = Math.Min(sale.NetAmount, Math.Abs(initialBalance));
                    }

                    customer.Balance += sale.NetAmount;
                    
                    sale.PaidAmount   = creditApplied; // Distributed below by cashToApply
                    sale.ChangeAmount = changeAmount;
                }
                else
                {
                    // Walk-in
                    // The actual cash kept by the store for this transaction
                    decimal netKeptCash = paidAmount - changeAmount;
                    
                    if (netKeptCash > sale.NetAmount)
                    {
                        decimal extraTip = netKeptCash - sale.NetAmount;
                        sale.NetAmount += extraTip;
                        sale.Profit    += extraTip;
                    }
                    else if (netKeptCash < sale.NetAmount)
                    {
                        decimal discountGiven = sale.NetAmount - netKeptCash;
                        if (netKeptCash >= 0)
                        {
                            sale.NetAmount -= discountGiven;
                            sale.Profit    -= discountGiven;
                        }
                    }

                    sale.PaidAmount   = netKeptCash;
                    sale.ChangeAmount = changeAmount;
                }

                db.Sales.Add(sale);
                db.SaveChanges();

                // Post-save payment cascading for registered customers
                if (customer != null && !customer.IsWalkIn && paidAmount > 0)
                {
                    decimal cashToApply = paidAmount - changeAmount; // Net cash given to the store

                    // If cashier chose not to add excess to credit for a registered customer,
                    // any cash above what is owed (customer.Balance) is forced onto the current sale's
                    // PaidAmount, rather than reducing customer.Balance further than 0.
                    decimal forcedOverpayment = 0;
                    if (!addExcessToCredit && cashToApply > Math.Max(0, customer.Balance))
                    {
                        forcedOverpayment = cashToApply - Math.Max(0, customer.Balance);
                        cashToApply -= forcedOverpayment; // Reduces cash to apply to credit subsystem
                    }

                    if (cashToApply > 0)
                    {
                        customer.Balance -= cashToApply;

                        db.Payments.Add(new Payment
                        {
                            CustomerId = customerId,
                            Amount     = cashToApply,
                            Date       = today,
                            Notes      = $"POS Payment for {invoiceNo}"
                        });

                        var outstandingSales = db.Sales
                            .Where(s => s.CustomerId == customerId && s.NetAmount > s.PaidAmount)
                            .OrderBy(s => s.Date)
                            .ToList();

                        decimal rem = cashToApply;
                        foreach (var s in outstandingSales)
                        {
                            if (rem <= 0) break;
                            decimal due = s.NetAmount - s.PaidAmount;
                            if (rem >= due) { rem -= due; s.PaidAmount += due; }
                            else            { s.PaidAmount += rem; rem = 0; }
                        }
                    }
                    else if (cashToApply < 0)
                    {
                        customer.Balance -= cashToApply;
                    }

                    if (forcedOverpayment > 0)
                    {
                        sale.NetAmount += forcedOverpayment;
                        sale.Profit    += forcedOverpayment;
                        sale.PaidAmount += forcedOverpayment;
                    }

                    db.SaveChanges();
                }

                transaction.Commit();

                var saved = db.Sales
                    .Include(s => s.Items).ThenInclude(i => i.Product)
                    .Include(s => s.Customer)
                    .Include(s => s.User)
                    .First(s => s.Id == sale.Id);

                return (true, "Sale completed.", saved);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Error creating sale: {ex.Message}", null);
            }
        }

        /// <summary>Legacy overload — keeps old call sites working (qty = total units, price = unit price).</summary>
        public (bool Success, string Message, Sale? Sale) CreateSale(
            int customerId,
            List<(int ProductId, int Qty, decimal UnitPrice)> items,
            decimal discount,
            decimal paidAmount,
            decimal changeAmount = 0,
            bool addExcessToCredit = false)
        {
            // Convert: LooseUnits = Qty (all units, no packs split needed for legacy)
            var converted = items.Select(i => (i.ProductId, 0, i.Qty, i.UnitPrice)).ToList();
            return CreateSale(customerId, converted, discount, paidAmount, changeAmount, addExcessToCredit);
        }

        public List<Sale> GetAll()
        {
            using var db = new AppDbContext();
            return db.Sales
                .Include(s => s.Customer)
                .Include(s => s.User)
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .OrderByDescending(s => s.Date)
                .ToList();
        }

        public Sale? GetById(int id)
        {
            using var db = new AppDbContext();
            return db.Sales
                .Include(s => s.Customer)
                .Include(s => s.User)
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .FirstOrDefault(s => s.Id == id);
        }

        public Sale? GetByInvoiceNo(string invoiceNo)
        {
            using var db = new AppDbContext();
            return db.Sales
                .Include(s => s.Customer)
                .Include(s => s.User)
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .FirstOrDefault(s => s.InvoiceNo == invoiceNo);
        }

        public List<Sale> GetByDateRange(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return db.Sales
                .Include(s => s.Customer)
                .Include(s => s.User)
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .Where(s => s.Date >= from && s.Date <= to)
                .OrderByDescending(s => s.Date)
                .AsEnumerable()
                .ToList();
        }

        public decimal GetTodaySales()
        {
            using var db = new AppDbContext();
            var today    = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);
            return db.Sales
                .Where(s => s.Date >= today && s.Date < tomorrow)
                .AsEnumerable()
                .Sum(s => s.NetAmount);
        }

        public List<Sale> GetSalesWithoutReturn()
        {
            using var db = new AppDbContext();
            var returnedSaleIds = db.Returns.Select(r => r.SaleId).Distinct().ToList();
            return db.Sales
                .Include(s => s.Customer)
                .Where(s => !returnedSaleIds.Contains(s.Id))
                .OrderByDescending(s => s.Date)
                .AsEnumerable()
                .ToList();
        }

        public decimal GetSalesTotal(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return db.Sales.Where(s => s.Date >= from && s.Date <= to).Select(s => s.NetAmount).AsEnumerable().Sum();
        }

        public decimal GetDiscountTotal(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return db.Sales.Where(s => s.Date >= from && s.Date <= to).Select(s => s.Discount).AsEnumerable().Sum();
        }
    }
}
