using MedicalStore.DAL;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class FinancialService
    {
        private readonly SaleService _saleService = new();
        private readonly PurchaseService _purchaseService = new();
        private readonly ReturnService _returnService = new();

        public (decimal Sales, decimal Purchases, decimal Refunds, decimal Discounts, decimal Expenses, decimal Profit) GetProfitLoss(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            
            var sales = db.Sales.Where(s => s.Date >= from && s.Date <= to).Select(s => s.NetAmount).AsEnumerable().Sum();
            var purchases = db.Purchases.Where(p => p.Date >= from && p.Date <= to).Select(p => p.TotalAmount).AsEnumerable().Sum();
            var refunds = db.Returns.Where(r => r.Date >= from && r.Date <= to).Select(r => r.RefundAmount).AsEnumerable().Sum();
            var discounts = db.Sales.Where(s => s.Date >= from && s.Date <= to).Select(s => s.Discount).AsEnumerable().Sum();
            
            // Accurate Gross Profit from the Profit column added in Sale entity
            var grossProfit = db.Sales.Where(s => s.Date >= from && s.Date <= to).Select(s => s.Profit).AsEnumerable().Sum();
            
            // Expenses
            var expenses = db.Expenses.Where(e => e.Date >= from && e.Date <= to).Select(e => e.Amount).AsEnumerable().Sum();
            
            // Net Profit = Gross Profit - Expenses
            // Do not subtract refunds, as returned items' profit loss is already reflected 
            // via the updated Sale.Profit recalculation in ReturnService.
            var netProfit = grossProfit - expenses;

            return (sales, purchases, refunds, discounts, expenses, netProfit);
        }

        public (decimal Sales, decimal Purchases, decimal Refunds, decimal Discounts, decimal Expenses, decimal Profit) GetDailyPL()
        {
            var today = DateTime.Now.Date;
            return GetProfitLoss(today, today.AddDays(1));
        }

        public (decimal Sales, decimal Purchases, decimal Refunds, decimal Discounts, decimal Expenses, decimal Profit) GetMonthlyPL()
        {
            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            return GetProfitLoss(monthStart, monthStart.AddMonths(1));
        }

        public (decimal Sales, decimal Purchases, decimal Refunds, decimal Discounts, decimal Expenses, decimal Profit) GetYearlyPL()
        {
            var now = DateTime.Now;
            var yearStart = new DateTime(now.Year, 1, 1);
            return GetProfitLoss(yearStart, yearStart.AddYears(1));
        }
    }
}
