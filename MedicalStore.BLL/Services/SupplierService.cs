using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class SupplierService
    {
        public List<Supplier> GetAll()
        {
            using var db = new AppDbContext();
            return db.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
        }

        public Supplier? GetById(int id)
        {
            using var db = new AppDbContext();
            return db.Suppliers.Find(id);
        }

        public (bool Success, string Message) Create(Supplier supplier)
        {
            using var db = new AppDbContext();
            supplier.CreatedAt = DateTime.Now;
            supplier.IsActive = true;
            supplier.Balance = supplier.OpeningBalance; // start from opening payable
            db.Suppliers.Add(supplier);
            db.SaveChanges();
            if (supplier.OpeningBalance != 0)
                AuditService.Log("Opening Balance", "Supplier", supplier.Id, $"Opening balance set to {supplier.OpeningBalance:N2}");
            return (true, "Supplier added successfully.");
        }

        public (bool Success, string Message) Update(Supplier supplier)
        {
            using var db = new AppDbContext();
            var existing = db.Suppliers.Find(supplier.Id);
            if (existing == null) return (false, "Supplier not found.");

            existing.Name = supplier.Name;
            existing.Phone = supplier.Phone;
            existing.Address = supplier.Address;

            if (supplier.OpeningBalance != existing.OpeningBalance)
            {
                decimal delta = supplier.OpeningBalance - existing.OpeningBalance;
                existing.Balance += delta;
                AuditService.Log("Opening Balance", "Supplier", existing.Id,
                    $"Opening {existing.OpeningBalance:N2} -> {supplier.OpeningBalance:N2}");
                existing.OpeningBalance = supplier.OpeningBalance;
            }

            db.SaveChanges();
            return (true, "Supplier updated successfully.");
        }

        public (bool Success, string Message) Delete(int id)
        {
            using var db = new AppDbContext();
            var supplier = db.Suppliers.Find(id);
            if (supplier == null) return (false, "Supplier not found.");
            supplier.IsActive = false;
            db.SaveChanges();
            return (true, "Supplier deactivated.");
        }
        public (bool Success, string Message) RecordPayment(int supplierId, decimal amount, string paymentMethod, string? notes)
        {
            using var db = new AppDbContext();
            var supplier = db.Suppliers.Find(supplierId);
            if (supplier == null) return (false, "Supplier not found.");

            supplier.Balance -= amount;

            // Distribute payment among unpaid invoices (FIFO)
            var unpaidInvoices = db.Purchases
                .Where(p => p.SupplierId == supplierId && p.DueAmount > 0)
                .OrderBy(p => p.Date)
                .ToList();

            decimal remainingToDistribute = amount;
            foreach (var inv in unpaidInvoices)
            {
                if (remainingToDistribute <= 0) break;

                decimal toPay = Math.Min(remainingToDistribute, inv.DueAmount);
                inv.PaidAmount += toPay;
                inv.DueAmount -= toPay;
                remainingToDistribute -= toPay;
            }
            
            db.SupplierPayments.Add(new SupplierPayment
            {
                SupplierId = supplierId,
                Amount = amount,
                Date = DateTime.Now,
                PaymentMethod = paymentMethod,
                Notes = notes
            });

            db.SaveChanges();
            AuditService.Log("Supplier Payment", "Supplier", supplierId,
                $"Paid {amount:N2} via {paymentMethod}. New balance {supplier.Balance:N2}. {notes}");
            return (true, $"Payment of {amount:C} recorded and supplier balance updated.");
        }

        /// <summary>
        /// Returns stock to a supplier (e.g. damaged / expired / over-ordered goods).
        /// Reduces inventory and reduces the supplier's payable by the value returned
        /// (valued at the product's current cost). Recorded in the supplier ledger.
        /// </summary>
        public (bool Success, string Message) ReturnToSupplier(
            int supplierId,
            List<(int ProductId, int Qty)> items,
            string? notes)
        {
            using var db = new AppDbContext();
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var supplier = db.Suppliers.Find(supplierId);
                if (supplier == null) return (false, "Supplier not found.");
                if (items == null || items.Count == 0) return (false, "No items to return.");

                decimal totalValue = 0;
                foreach (var item in items)
                {
                    if (item.Qty <= 0) continue;
                    var product = db.Products.Find(item.ProductId);
                    if (product == null) return (false, $"Product ID {item.ProductId} not found.");

                    int available = product.StockUnits > 0 ? product.StockUnits : product.Quantity;
                    if (available < item.Qty)
                        return (false, $"Insufficient stock to return {product.Name}. Available: {available}.");

                    product.StockUnits = available - item.Qty;
                    product.Quantity   = product.StockUnits;

                    totalValue += item.Qty * product.PurchasePrice;
                }

                if (totalValue <= 0) return (false, "Return value is zero.");

                // Reduce what we owe the supplier and record it in the ledger
                // (Debit side of the supplier ledger, like a payment made in goods).
                supplier.Balance -= totalValue;

                db.SupplierPayments.Add(new SupplierPayment
                {
                    SupplierId    = supplierId,
                    Amount        = totalValue,
                    Date          = DateTime.Now,
                    PaymentMethod = "Return",
                    Notes         = string.IsNullOrWhiteSpace(notes) ? "Stock returned to supplier" : notes
                });

                db.SaveChanges();
                transaction.Commit();
                AuditService.Log("Supplier Return", "Supplier", supplierId,
                    $"Returned stock worth {totalValue:N2}. New balance {supplier.Balance:N2}. {notes}");
                return (true, $"Returned stock worth {totalValue:N2}. Supplier payable reduced.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return (false, $"Error processing supplier return: {ex.Message}");
            }
        }
    }
}
