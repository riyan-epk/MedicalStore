using Microsoft.EntityFrameworkCore;
using MedicalStore.DAL.Entities;
using MedicalStore.Common.Enums;
using MedicalStore.Common.Constants;

namespace MedicalStore.DAL
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Supplier> Suppliers { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Purchase> Purchases { get; set; } = null!;
        public DbSet<PurchaseItem> PurchaseItems { get; set; } = null!;
        public DbSet<Sale> Sales { get; set; } = null!;
        public DbSet<SaleItem> SaleItems { get; set; } = null!;
        public DbSet<Return> Returns { get; set; } = null!;
        public DbSet<ReturnItem> ReturnItems { get; set; } = null!;
        public DbSet<ReplacementItem> ReplacementItems { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<SupplierPayment> SupplierPayments { get; set; } = null!;
        public DbSet<Expense> Expenses { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;

        private readonly string _dbPath;

        public AppDbContext()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = Path.Combine(appDir, AppConstants.DbFileName);
        }

        public AppDbContext(string dbPath)
        {
            _dbPath = dbPath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indexes
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Name);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode);

            modelBuilder.Entity<Sale>()
                .HasIndex(s => s.InvoiceNo)
                .IsUnique();

            modelBuilder.Entity<Sale>()
                .HasIndex(s => s.Date);

            // Relationships
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.User)
                .WithMany(u => u.Sales)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Return>()
                .HasOne(r => r.Sale)
                .WithMany(s => s.Returns)
                .HasForeignKey(r => r.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Return>()
                .HasOne(r => r.Customer)
                .WithMany(c => c.Returns)
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Purchases)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Supplier)
                .WithMany()
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed default admin user (password: admin123)
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = 1,
                Username = AppConstants.DefaultAdminUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AppConstants.DefaultAdminPassword),
                FullName = "System Administrator",
                Role = UserRole.Admin,
                Permissions = Permission.AdminAll,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1)
            });

            // Seed Walk-in Customer
            modelBuilder.Entity<Customer>().HasData(new Customer
            {
                Id = 1,
                Name = AppConstants.WalkInCustomerName,
                Phone = "",
                Address = "",
                Balance = 0,
                IsWalkIn = true,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1)
            });

            // Seed default categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Tablet" },
                new Category { Id = 2, Name = "Capsule" },
                new Category { Id = 3, Name = "Syrup" },
                new Category { Id = 4, Name = "Injection" },
                new Category { Id = 5, Name = "Cream/Ointment" },
                new Category { Id = 6, Name = "Drops" },
                new Category { Id = 7, Name = "Inhaler" },
                new Category { Id = 8, Name = "Surgical" },
                new Category { Id = 9, Name = "Others" }
            );

            // Seed Default Supplier
            modelBuilder.Entity<Supplier>().HasData(
                new Supplier { Id = 1, Name = "Default Supplier", Phone = "", Address = "", Balance = 0, IsActive = true, CreatedAt = new DateTime(2024, 1, 1) }
            );
        }

        public void EnsureCreated()
        {
            Database.EnsureCreated();

            // Run simple migrations for new columns
            try { Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN SupplierId INTEGER NOT NULL DEFAULT 1;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE Sales ADD COLUMN Profit TEXT NOT NULL DEFAULT '0';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE Sales ADD COLUMN Tax TEXT NOT NULL DEFAULT '0';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE SaleItems ADD COLUMN PurchasePrice TEXT NOT NULL DEFAULT '0';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE Returns ADD COLUMN ReturnType TEXT NOT NULL DEFAULT 'Refund';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE Returns ADD COLUMN ReplaceAmount TEXT NOT NULL DEFAULT '0';"); } catch { }
            
            // New tables
            try { Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS ReplacementItems (Id INTEGER PRIMARY KEY AUTOINCREMENT, ReturnId INTEGER NOT NULL, ProductId INTEGER NOT NULL, Quantity INTEGER NOT NULL, UnitPrice TEXT NOT NULL, Total TEXT NOT NULL, FOREIGN KEY(ReturnId) REFERENCES Returns(Id), FOREIGN KEY(ProductId) REFERENCES Products(Id));"); } catch { }
            try { Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS SupplierPayments (Id INTEGER PRIMARY KEY AUTOINCREMENT, SupplierId INTEGER NOT NULL, Amount TEXT NOT NULL, Date TEXT NOT NULL, Notes TEXT, FOREIGN KEY(SupplierId) REFERENCES Suppliers(Id));"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE SupplierPayments ADD COLUMN PaymentMethod TEXT NOT NULL DEFAULT 'Cash';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE Purchases ADD COLUMN PaidAmount TEXT NOT NULL DEFAULT '0';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE Purchases ADD COLUMN DueAmount TEXT NOT NULL DEFAULT '0';"); } catch { }
            try { Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS Expenses (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Amount TEXT NOT NULL, Date TEXT NOT NULL, Description TEXT);"); } catch { }
            try { Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT);"); } catch { }

            // Re-seed accidently deleted categories
            try { Database.ExecuteSqlRaw("INSERT INTO Categories (Name) SELECT 'Syrups' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE Name IN ('Syrup', 'Syrups'));"); } catch { }
            try { Database.ExecuteSqlRaw("INSERT INTO Categories (Name) SELECT 'Tablets' WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE Name IN ('Tablet', 'Tablets'));"); } catch { }

            // Reset Walk-in balance to zero
            try { Database.ExecuteSqlRaw("UPDATE Customers SET Balance = 0 WHERE IsWalkIn = 1;"); } catch { }

            // ── Pack-Unit Inventory columns (Products) ────────────────────────────
            try { Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN UnitsPerPack INTEGER NOT NULL DEFAULT 1;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN PackPrice TEXT NOT NULL DEFAULT '0';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN UnitPrice TEXT NOT NULL DEFAULT '0';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN StockUnits INTEGER NOT NULL DEFAULT 0;"); } catch { }
            // Seed StockUnits from Quantity for existing products
            try { Database.ExecuteSqlRaw("UPDATE Products SET StockUnits = Quantity WHERE StockUnits = 0 AND Quantity > 0;"); } catch { }
            // Seed UnitPrice from SalePrice for existing products (1 unit = 1 "pack")
            try { Database.ExecuteSqlRaw("UPDATE Products SET UnitPrice = SalePrice, PackPrice = SalePrice WHERE UnitPrice = 0 AND SalePrice > 0;"); } catch { }

            // ── Pack-Unit columns (PurchaseItems) ────────────────────────────────
            try { Database.ExecuteSqlRaw("ALTER TABLE PurchaseItems ADD COLUMN PackQty INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE PurchaseItems ADD COLUMN UnitsPerPack INTEGER NOT NULL DEFAULT 1;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE PurchaseItems ADD COLUMN PackPrice TEXT NOT NULL DEFAULT '0';"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE PurchaseItems ADD COLUMN TotalUnits INTEGER NOT NULL DEFAULT 0;"); } catch { }
            // Seed existing purchase items: PackQty = Quantity, TotalUnits = Quantity, PackPrice = UnitPrice
            try { Database.ExecuteSqlRaw("UPDATE PurchaseItems SET PackQty = Quantity, TotalUnits = Quantity, PackPrice = UnitPrice WHERE PackQty = 0 AND Quantity > 0;"); } catch { }

            // ── Pack-Unit columns (SaleItems) ─────────────────────────────────────
            try { Database.ExecuteSqlRaw("ALTER TABLE SaleItems ADD COLUMN Packs INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE SaleItems ADD COLUMN LooseUnits INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE SaleItems ADD COLUMN TotalUnits INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try { Database.ExecuteSqlRaw("ALTER TABLE SaleItems ADD COLUMN UnitsPerPack INTEGER NOT NULL DEFAULT 1;"); } catch { }
            // Seed existing sale items: TotalUnits = Quantity, LooseUnits = Quantity (since all old sales were unit-based)
            try { Database.ExecuteSqlRaw("UPDATE SaleItems SET TotalUnits = Quantity, LooseUnits = Quantity WHERE TotalUnits = 0 AND Quantity > 0;"); } catch { }
        }
    }
}
