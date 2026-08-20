using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalStore.DAL.Entities
{
    public class Purchase
    {
        [Key]
        public int Id { get; set; }

        public int SupplierId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DueAmount { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        // Purchase-order lifecycle: "Ordered" (placed, not received – no stock yet),
        // "PartiallyReceived", "Received" (fully received), "Cancelled".
        // Quick immediate purchases are created directly as "Received".
        [MaxLength(30)]
        public string Status { get; set; } = "Received";

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation
        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }

        public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    }

    public class PurchaseItem
    {
        [Key]
        public int Id { get; set; }

        public int PurchaseId { get; set; }
        public int ProductId { get; set; }

        // ── Legacy field (kept for backward compat) ──────────────────────────────
        // When EnablePackUnitSelling is OFF: Quantity = number of units purchased.
        // When EnablePackUnitSelling is ON:  Quantity = PackQty (for display compat).
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // ── Pack-Unit fields ─────────────────────────────────────────────────────
        /// <summary>Number of packs purchased (e.g. 5 strips).</summary>
        public int PackQty { get; set; }

        /// <summary>Snapshot of UnitsPerPack at purchase time (e.g. 10 tablets per strip).</summary>
        public int UnitsPerPack { get; set; } = 1;

        /// <summary>Price paid per pack.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PackPrice { get; set; }

        /// <summary>Total units ordered = PackQty × UnitsPerPack.</summary>
        public int TotalUnits { get; set; }

        /// <summary>Units actually received into stock so far (for partial receiving).
        /// Equals TotalUnits once fully received.</summary>
        public int ReceivedUnits { get; set; }

        // Navigation
        [ForeignKey(nameof(PurchaseId))]
        public Purchase? Purchase { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }
    }
}
