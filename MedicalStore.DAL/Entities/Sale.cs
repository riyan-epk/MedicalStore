using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalStore.DAL.Entities
{
    public class Sale
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string InvoiceNo { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public int UserId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Tax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ChangeAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DueAmount 
        { 
            get => NetAmount - PaidAmount; 
            set { } 
        }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Profit { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
        public ICollection<Return> Returns { get; set; } = new List<Return>();
    }

    public class SaleItem
    {
        [Key]
        public int Id { get; set; }

        public int SaleId { get; set; }
        public int ProductId { get; set; }

        // ── Legacy field (kept for backward compat) ──────────────────────────────
        // When EnablePackUnitSelling is OFF: Quantity = units sold.
        // When EnablePackUnitSelling is ON:  Quantity = TotalUnits (Packs*UnitsPerPack + LooseUnits).
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // ── Pack-Unit fields ─────────────────────────────────────────────────────
        /// <summary>Number of full packs sold (e.g. 2 strips).</summary>
        public int Packs { get; set; }

        /// <summary>Number of individual loose units sold (e.g. 3 tablets).</summary>
        public int LooseUnits { get; set; }

        /// <summary>
        /// Total units deducted from stock = Packs × UnitsPerPack + LooseUnits.
        /// Equal to Quantity for backward compatibility.
        /// </summary>
        public int TotalUnits { get; set; }

        /// <summary>Snapshot of product's UnitsPerPack at the time of sale.</summary>
        public int UnitsPerPack { get; set; } = 1;

        // Navigation
        [ForeignKey(nameof(SaleId))]
        public Sale? Sale { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }
    }
}
