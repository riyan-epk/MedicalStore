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
            db.Suppliers.Add(supplier);
            db.SaveChanges();
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
            return (true, $"Payment of {amount:C} recorded and supplier balance updated.");
        }
    }
}
