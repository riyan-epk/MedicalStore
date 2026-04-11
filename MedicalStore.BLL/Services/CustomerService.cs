using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class CustomerService
    {
        public List<Customer> GetAll()
        {
            using var db = new AppDbContext();
            return db.Customers.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
        }

        public Customer? GetById(int id)
        {
            using var db = new AppDbContext();
            return db.Customers.Find(id);
        }

        public Customer GetWalkIn()
        {
            using var db = new AppDbContext();
            return db.Customers.First(c => c.IsWalkIn);
        }

        public (bool Success, string Message) Create(Customer customer)
        {
            using var db = new AppDbContext();
            customer.CreatedAt = DateTime.Now;
            customer.IsActive = true;
            db.Customers.Add(customer);
            db.SaveChanges();
            return (true, "Customer added successfully.");
        }

        public (bool Success, string Message) Update(Customer customer)
        {
            using var db = new AppDbContext();
            var existing = db.Customers.Find(customer.Id);
            if (existing == null) return (false, "Customer not found.");

            existing.Name = customer.Name;
            existing.Phone = customer.Phone;
            existing.Address = customer.Address;
            db.SaveChanges();
            return (true, "Customer updated successfully.");
        }

        public (bool Success, string Message) Delete(int id)
        {
            try
            {
                using var db = new AppDbContext();
                var customer = db.Customers.Find(id);
                if (customer == null) return (false, "Customer not found.");
                if (customer.IsWalkIn) return (false, "Cannot delete Walk-in customer.");

                customer.IsActive = false;
                db.SaveChanges();
                return (true, "Customer deleted successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting customer: {ex.Message}");
            }
        }

        public (bool Success, string Message) DeleteAllCustomers()
        {
            try
            {
                using var db = new AppDbContext();
                using var transaction = db.Database.BeginTransaction();
                try
                {
                    // Find all non-walk-in customers
                    var customersToDelete = db.Customers.Where(c => !c.IsWalkIn).ToList();
                    var customerIds = customersToDelete.Select(c => c.Id).ToList();

                    // Remove related data (handle safely)
                    // 1. Delete Payments
                    var payments = db.Payments.Where(p => customerIds.Contains(p.CustomerId));
                    db.Payments.RemoveRange(payments);

                    // 2. Delete Return Items and Returns
                    var returns = db.Returns.Where(r => customerIds.Contains(r.CustomerId)).Include(r => r.Items).ToList();
                    foreach (var ret in returns)
                    {
                        db.ReturnItems.RemoveRange(ret.Items);
                    }
                    db.Returns.RemoveRange(returns);

                    // 3. Delete Sales and Sale Items
                    var sales = db.Sales.Where(s => customerIds.Contains(s.CustomerId)).Include(s => s.Items).ToList();
                    foreach (var sale in sales)
                    {
                        db.SaleItems.RemoveRange(sale.Items);
                    }
                    db.Sales.RemoveRange(sales);

                    // 4. Delete Customers
                    db.Customers.RemoveRange(customersToDelete);

                    db.SaveChanges();
                    transaction.Commit();
                    return (true, $"{customersToDelete.Count} customers and their related data have been removed.");
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting all customers: {ex.Message}");
            }
        }

        public List<Customer> GetBalances()
        {
            using var db = new AppDbContext();
            // Use AsEnumerable() before OrderBy to avoid SQLite's
            // "does not support expressions of type 'decimal' in ORDER BY" error.
            return db.Customers
                .Where(c => c.IsActive && c.Balance != 0)
                .AsEnumerable()  // switch to LINQ-to-Objects
                .OrderByDescending(c => c.Balance)
                .ToList();
        }

        public decimal GetTotalDues()
        {
            using var db = new AppDbContext();

            
            var balances = db.Customers
                             .Where(c => c.IsActive)
                             .Select(c => c.Balance)   
                             .ToList();

            decimal total = balances.Sum();

            return total;
        }
        public (bool Success, string Message) RecordPayment(int customerId, decimal amount, string? notes)
        {
            using var db = new AppDbContext();
            using var tx = db.Database.BeginTransaction();
            try
            {
                var customer = db.Customers.Find(customerId);
                if (customer == null) return (false, "Customer not found.");
                if (customer.IsWalkIn) return (false, "Payment cannot be recorded for Walk-in customer.");

                // ── 1. Update customer balance ────────────────────────────────
                customer.Balance -= amount;

                // ── 2. Record the payment entry ───────────────────────────────
                db.Payments.Add(new Payment
                {
                    CustomerId = customerId,
                    Amount     = amount,
                    Date       = DateTime.Now,
                    Notes      = notes
                });

                // ── 3. Cascade payment into individual Sale.DueAmount records ─
                // Only apply when the customer is actually paying a positive due
                // (amount > 0 = customer paying us).
                if (amount > 0)
                {
                    // Get all outstanding sales for this customer, oldest first
                    var outstandingSales = db.Sales
                        .Where(s => s.CustomerId == customerId && s.NetAmount > s.PaidAmount)
                        .OrderBy(s => s.Date)
                        .ToList();

                    decimal remaining = amount;

                    foreach (var sale in outstandingSales)
                    {
                        if (remaining <= 0) break;

                        decimal currentDue = sale.NetAmount - sale.PaidAmount;
                        if (remaining >= currentDue)
                        {
                            // This sale is fully settled by the payment
                            remaining        -= currentDue;
                            sale.PaidAmount  += currentDue;
                        }
                        else
                        {
                            // Partial payment covers part of this sale's due
                            sale.PaidAmount  += remaining;
                            remaining         = 0;
                        }
                    }
                }

                db.SaveChanges();
                tx.Commit();
                return (true, $"Payment of Rs {amount:N2} recorded successfully.");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return (false, $"Error recording payment: {ex.Message}");
            }
        }

        public List<MedicalStore.Common.Models.LedgerEntry> GetLedger(int customerId)
        {
            using var db = new AppDbContext();
            var ledger = new List<MedicalStore.Common.Models.LedgerEntry>();

            var sales = db.Sales.Where(s => s.CustomerId == customerId).ToList();
            var returns = db.Returns.Where(r => r.CustomerId == customerId).ToList();
            var payments = db.Payments.Where(p => p.CustomerId == customerId).ToList();

            foreach (var s in sales)
                ledger.Add(new MedicalStore.Common.Models.LedgerEntry { Date = s.Date, Type = "Sale", Reference = s.InvoiceNo, Debit = s.NetAmount, Credit = s.PaidAmount, Notes = "POS Sale" });

            foreach (var r in returns)
                ledger.Add(new MedicalStore.Common.Models.LedgerEntry { Date = r.Date, Type = r.ReturnType, Reference = $"RET-{r.Id}", Debit = r.ReplaceAmount, Credit = r.TotalAmount + r.RefundAmount, Notes = r.Notes });

            foreach (var p in payments)
                ledger.Add(new MedicalStore.Common.Models.LedgerEntry { Date = p.Date, Type = "Payment", Reference = $"PAY-{p.Id}", Debit = 0, Credit = p.Amount, Notes = p.Notes });

            var sorted = ledger.OrderBy(l => l.Date).ToList();
            decimal runningBalance = 0;
            foreach (var entry in sorted)
            {
                runningBalance += (entry.Debit - entry.Credit);
                entry.BalanceAfter = runningBalance;
            }

            return sorted.OrderByDescending(l => l.Date).ToList();
        }
    }
}
