using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace MedicalStore.BLL.Services
{
    public class ReportService
    {
        public List<LedgerEntry> GetCustomerTransactionReport(int customerId, DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            var ledger = new List<LedgerEntry>();

            var sales = db.Sales
                .Where(s => s.CustomerId == customerId && s.Date >= from && s.Date <= to)
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .ToList();

            var returns = db.Returns
                .Where(r => r.CustomerId == customerId && r.Date >= from && r.Date <= to)
                .Include(r => r.Items).ThenInclude(i => i.Product)
                .Include(r => r.ReplacementItems).ThenInclude(i => i.Product)
                .ToList();

            var payments = db.Payments
                .Where(p => p.CustomerId == customerId && p.Date >= from && p.Date <= to)
                .ToList();

            foreach (var s in sales)
            {
                var productList = string.Join(", ", s.Items.Select(i => $"{i.Product?.Name} (x{i.Quantity})"));
                ledger.Add(new LedgerEntry
                {
                    Date = s.Date,
                    Type = "Sale",
                    Reference = s.InvoiceNo,
                    Debit = s.NetAmount,
                    Credit = 0, // cash is recorded via Payment rows – see CustomerService.GetLedger
                    Notes = productList
                });
            }

            foreach (var r in returns)
            {
                var returnedProducts = string.Join(", ", r.Items.Select(i => $"{i.Product?.Name} (x{i.Quantity})"));
                var replacementProducts = r.ReplacementItems.Any() 
                    ? " | Replaced with: " + string.Join(", ", r.ReplacementItems.Select(i => $"{i.Product?.Name} (x{i.Quantity})")) 
                    : "";
                
                ledger.Add(new LedgerEntry
                {
                    Date = r.Date,
                    Type = r.ReturnType,
                    Reference = $"RET-{r.Id}",
                    Debit = r.RefundAmount, // goods value already reflected in the Sale's NetAmount
                    Credit = 0,
                    Notes = $"Returned: {returnedProducts}{replacementProducts}"
                });
            }

            foreach (var p in payments)
                ledger.Add(new LedgerEntry 
                { 
                    Date = p.Date, 
                    Type = "Payment", 
                    Reference = $"PAY-{p.Id}", 
                    Debit = 0, 
                    Credit = p.Amount, 
                    Notes = p.Notes 
                });

            return ledger.OrderBy(l => l.Date).ToList();
        }

        public List<LedgerEntry> GetSupplierTransactionReport(int supplierId, DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            var ledger = new List<LedgerEntry>();

            var purchases = db.Purchases
                .Where(p => p.SupplierId == supplierId && p.Date >= from && p.Date <= to)
                .Include(p => p.Items).ThenInclude(i => i.Product)
                .ToList();

            var payments = db.SupplierPayments
                .Where(p => p.SupplierId == supplierId && p.Date >= from && p.Date <= to)
                .ToList();

            foreach (var p in purchases)
            {
                var productList = string.Join(", ", p.Items.Select(i => $"{i.Product?.Name} (x{i.Quantity})"));
                ledger.Add(new LedgerEntry 
                { 
                    Date = p.Date, 
                    Type = "Purchase", 
                    Reference = $"PUR-{p.Id}", 
                    Debit = 0, // In supplier context, Credit is what we owe, Debit is what we pay
                    Credit = p.TotalAmount, 
                    Notes = productList 
                });
            }

            foreach (var pay in payments)
            {
                ledger.Add(new LedgerEntry
                {
                    Date = pay.Date,
                    Type = pay.PaymentMethod == "Return" ? "Return" : "Payment",
                    Reference = $"SPAY-{pay.Id}",
                    Debit = pay.Amount,
                    Credit = 0,
                    Notes = pay.Notes
                });
            }

            return ledger.OrderBy(l => l.Date).ToList();
        }
    }
}
