using MedicalStore.DAL;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace MedicalStore.BLL.Services
{
    public class ProductService
    {
        public List<Product> GetAll()
        {
            using var db = new AppDbContext();
            return db.Products.Include(p => p.Category)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .AsEnumerable()
                .ToList();
        }

        public List<Product> Search(string query)
        {
            using var db = new AppDbContext();
            query = query.ToLower();
            return db.Products.Include(p => p.Category)
                .Where(p => p.IsActive && (
                    p.Name.ToLower().Contains(query) ||
                    p.Company.ToLower().Contains(query) ||
                    p.Category.Name.ToLower().Contains(query) ||
                    (p.Barcode != null && p.Barcode.Contains(query)) ||
                    p.BatchNo.ToLower().Contains(query)))
                .OrderBy(p => p.Name)
                .AsEnumerable()
                .ToList();
        }

        public Product? GetById(int id)
        {
            using var db = new AppDbContext();
            return db.Products.Include(p => p.Category).FirstOrDefault(p => p.Id == id);
        }

        public (bool Success, string Message) Create(Product product)
        {
            using var db = new AppDbContext();
            product.CreatedAt = DateTime.Now;
            product.IsActive = true;

            // Ensure pack/unit fields are consistent
            NormalizePackUnitFields(product);

            db.Products.Add(product);
            db.SaveChanges();
            return (true, "Product added successfully.");
        }

        public (bool Success, string Message) Update(Product product)
        {
            using var db = new AppDbContext();
            var existing = db.Products.Find(product.Id);
            if (existing == null) return (false, "Product not found.");

            existing.Name = product.Name;
            existing.CategoryId = product.CategoryId;
            existing.SupplierId = product.SupplierId;
            existing.Company = product.Company;
            existing.BatchNo = product.BatchNo;
            existing.PurchasePrice = product.PurchasePrice;
            existing.SalePrice = product.SalePrice;
            existing.Quantity = product.Quantity;
            existing.MinStockLevel = product.MinStockLevel;
            existing.ManufDate = product.ManufDate;
            existing.ExpiryDate = product.ExpiryDate;
            existing.Barcode = product.Barcode;

            // Pack-Unit fields
            existing.UnitsPerPack = product.UnitsPerPack;
            existing.PackPrice = product.PackPrice;
            existing.UnitPrice = product.UnitPrice;
            existing.StockUnits = product.StockUnits;

            NormalizePackUnitFields(existing);

            db.SaveChanges();
            return (true, "Product updated successfully.");
        }

        public (bool Success, string Message) Delete(int id)
        {
            using var db = new AppDbContext();
            var product = db.Products.Find(id);
            if (product == null) return (false, "Product not found.");
            product.IsActive = false;
            db.SaveChanges();
            return (true, "Product deleted successfully.");
        }

        public List<Product> GetLowStock()
        {
            using var db = new AppDbContext();
            return db.Products.Include(p => p.Category)
                .Where(p => p.IsActive && p.StockUnits <= p.MinStockLevel)
                .OrderBy(p => p.StockUnits)
                .AsEnumerable()
                .ToList();
        }

        public List<Product> GetNearExpiry()
        {
            using var db = new AppDbContext();
            var threshold = DateTime.Now.AddDays(AppConstants.NearExpiryDays);
            return db.Products.Include(p => p.Category)
                .Where(p => p.IsActive && p.ExpiryDate != null && p.ExpiryDate <= threshold && p.ExpiryDate > DateTime.Now)
                .OrderBy(p => p.ExpiryDate)
                .AsEnumerable()
                .ToList();
        }

        public List<Product> GetExpired()
        {
            using var db = new AppDbContext();
            return db.Products.Include(p => p.Category)
                .Where(p => p.IsActive && p.ExpiryDate != null && p.ExpiryDate <= DateTime.Now)
                .OrderBy(p => p.ExpiryDate)
                .AsEnumerable()
                .ToList();
        }

        public int GetTotalInStock()
        {
            using var db = new AppDbContext();
            return db.Products.Where(p => p.IsActive).Count();
        }

        public List<Category> GetCategories()
        {
            using var db = new AppDbContext();
            return db.Categories.OrderBy(c => c.Name).ToList();
        }

        public (bool Success, string Message) AddCategory(string name)
        {
            using var db = new AppDbContext();
            if (db.Categories.Any(c => c.Name == name))
                return (false, "Category already exists.");
            db.Categories.Add(new Category { Name = name });
            db.SaveChanges();
            return (true, "Category added.");
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Ensures UnitPrice, PackPrice, StockUnits and Quantity remain consistent.
        /// Called on Create and Update.
        /// </summary>
        private static void NormalizePackUnitFields(Product p)
        {
            if (p.UnitsPerPack <= 0) p.UnitsPerPack = 1;

            // Derive UnitPrice from PackPrice
            if (p.PackPrice > 0)
                p.UnitPrice = p.PackPrice / p.UnitsPerPack;
            else if (p.UnitPrice > 0)
                p.PackPrice = p.UnitPrice * p.UnitsPerPack;
            else if (p.SalePrice > 0)
            {
                p.UnitPrice = p.SalePrice;
                p.PackPrice = p.SalePrice * p.UnitsPerPack;
            }

            // Sync legacy and new
            if (p.StockUnits == 0 && p.Quantity > 0)
            {
                p.StockUnits = p.Quantity;
            }
            else
            {
                p.Quantity = p.StockUnits;
            }
        }
    }
}
