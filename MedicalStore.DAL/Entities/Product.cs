using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalStore.DAL.Entities
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public int CategoryId { get; set; }
        
        public int SupplierId { get; set; }

        [MaxLength(200)]
        public string Company { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BatchNo { get; set; } = string.Empty;

        // ── Legacy / backward-compat field (kept in sync with StockUnits) ──────
        // When EnablePackUnitSelling is OFF this is the primary stock field.
        // When EnablePackUnitSelling is ON, StockUnits is authoritative – but
        // Quantity is always updated to equal StockUnits so old queries work.
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SalePrice { get; set; }

        public int Quantity { get; set; }

        // ── Pack-Unit fields ─────────────────────────────────────────────────────
        /// <summary>How many smallest units fit in one pack (strip, box, bottle…). Default = 1.</summary>
        public int UnitsPerPack { get; set; } = 1;

        /// <summary>Price for one full pack. When EnablePackUnitSelling is OFF this stays 0.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PackPrice { get; set; }

        /// <summary>
        /// Price per single unit = PackPrice / UnitsPerPack.
        /// When the feature is OFF, this is the same as SalePrice (for a single unit = 1 pack).
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Stock in the smallest unit.  Authoritative when EnablePackUnitSelling = true.
        /// Kept equal to <see cref="Quantity"/> at all times for backward compatibility.
        /// </summary>
        public int StockUnits { get; set; }

        // ── Not mapped – display helper ──────────────────────────────────────────
        [NotMapped]
        public string StockDisplay
        {
            get
            {
                int totalUnits = StockUnits > 0 ? StockUnits : Quantity;
                if (UnitsPerPack > 1)
                {
                    int packs = totalUnits / UnitsPerPack;
                    int loose = totalUnits % UnitsPerPack;
                    if (packs > 0)
                        return $"{totalUnits} units ({packs} packs{(loose > 0 ? $" + {loose} units" : "")})";
                }
                return $"{totalUnits} units";
            }
        }

        // ── Other fields ─────────────────────────────────────────────────────────
        public int MinStockLevel { get; set; } = 10;

        public DateTime? ManufDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(100)]
        public string? Barcode { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey(nameof(CategoryId))]
        public Category? Category { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
        public ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();
    }
}
