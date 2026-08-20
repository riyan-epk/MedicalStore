using MedicalStore.DAL;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class DashboardService
    {
        private readonly SaleService _saleService = new();
        private readonly ProductService _productService = new();
        private readonly CustomerService _customerService = new();
        private readonly PurchaseService _purchaseService = new();
        private readonly ReturnService _returnService = new();

        public decimal GetTodaySales() => _saleService.GetTodaySales();
        public decimal GetTodayProfit()
        {
            using var db = new AppDbContext();
            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);
            var profit = db.Sales.Where(s => s.Date >= today && s.Date < tomorrow).Select(s => s.Profit).AsEnumerable().Sum();
            profit += db.Returns.Where(r => r.Date >= today && r.Date < tomorrow).Select(r => r.ProfitImpact).AsEnumerable().Sum();
            var expenses = db.Expenses.Where(e => e.Date >= today && e.Date < tomorrow).Select(e => e.Amount).AsEnumerable().Sum();
            return profit - expenses;
        }
        public decimal GetTodayPurchases()
        {
            using var db = new AppDbContext();
            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);
            return db.Purchases.Where(p => p.Date >= today && p.Date < tomorrow).Select(p => p.TotalAmount).AsEnumerable().Sum();
        }
        public decimal GetTodayReturns()
        {
            using var db = new AppDbContext();
            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);
            return db.Returns.Where(r => r.Date >= today && r.Date < tomorrow).Select(r => r.TotalAmount).AsEnumerable().Sum();
        }
        public decimal GetTodayRefunds()
        {
            using var db = new AppDbContext();
            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);
            return db.Returns.Where(r => r.Date >= today && r.Date < tomorrow).Select(r => r.RefundAmount).AsEnumerable().Sum();
        }
        public int GetTotalProducts() => _productService.GetTotalInStock();
        public int GetLowStockCount() => _productService.GetLowStock().Count;
        public int GetNearExpiryCount() => _productService.GetNearExpiry().Count;
        public decimal GetTotalDues() => _customerService.GetTotalDues();

        public decimal GetMonthlyProfit()
        {
            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            using var db = new AppDbContext();
            
            var profit = db.Sales.Where(s => s.Date >= monthStart && s.Date < monthEnd).Select(s => s.Profit).AsEnumerable().Sum();
            profit += db.Returns.Where(r => r.Date >= monthStart && r.Date < monthEnd).Select(r => r.ProfitImpact).AsEnumerable().Sum();
            var expenses = db.Expenses.Where(e => e.Date >= monthStart && e.Date < monthEnd).Select(e => e.Amount).AsEnumerable().Sum();

            return profit - expenses;
        }

        public List<(DateTime Date, decimal Amount)> GetDailySales(int days = 30)
        {
            using var db = new AppDbContext();
            var from = DateTime.Now.Date.AddDays(-days + 1);
            var result = new List<(DateTime, decimal)>();

            var salesData = db.Sales
                .Where(s => s.Date >= from)
                .Select(s => new { s.Date, s.NetAmount })
                .ToList();

            for (int i = 0; i < days; i++)
            {
                var date = from.AddDays(i);
                var nextDate = date.AddDays(1);
                var amount = salesData.Where(s => s.Date >= date && s.Date < nextDate).Sum(s => s.NetAmount);
                result.Add((date, amount));
            }
            return result;
        }

        public List<(string Month, decimal Sales, decimal Purchases)> GetMonthlySalesVsPurchases(int months = 6)
        {
            using var db = new AppDbContext();
            var result = new List<(string, decimal, decimal)>();
            var now = DateTime.Now;
            var fromDate = new DateTime(now.Year, now.Month, 1).AddMonths(-months + 1);

            var dbSales = db.Sales.Where(s => s.Date >= fromDate).Select(s => new { s.Date, s.NetAmount }).ToList();
            var dbPurchases = db.Purchases.Where(s => s.Date >= fromDate).Select(p => new { p.Date, p.TotalAmount }).ToList();

            for (int i = months - 1; i >= 0; i--)
            {
                var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1);

                var sales = dbSales.Where(s => s.Date >= monthStart && s.Date < monthEnd).Sum(s => s.NetAmount);
                var purchases = dbPurchases.Where(p => p.Date >= monthStart && p.Date < monthEnd).Sum(p => p.TotalAmount);

                result.Add((monthStart.ToString("MMM yyyy"), sales, purchases));
            }
            return result;
        }

        public List<(string Category, decimal Amount)> GetCategorySales()
        {
            using var db = new AppDbContext();
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            
            var items = db.SaleItems
                .Include(si => si.Product).ThenInclude(p => p!.Category)
                .Include(si => si.Sale)
                .Where(si => si.Sale!.Date >= monthStart)
                .Select(si => new { 
                    CategoryName = si.Product!.Category!.Name, 
                    Total = si.Total 
                })
                .ToList();

            var grouped = items
                .GroupBy(x => x.CategoryName)
                .Select(g => (Category: g.Key, Amount: g.Sum(x => x.Total)))
                .ToList();
                
            return grouped;
        }
    }
}
