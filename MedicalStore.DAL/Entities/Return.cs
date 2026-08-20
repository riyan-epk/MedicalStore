using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalStore.DAL.Entities
{
    public class Return
    {
        [Key]
        public int Id { get; set; }

        public int SaleId { get; set; }
        public int CustomerId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; }

        [MaxLength(50)]
        public string ReturnType { get; set; } = "Refund";

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReplaceAmount { get; set; }

        // Net profit change caused by this return, dated to the return (not the original sale).
        // Set only by ProcessReturn (linked returns); direct/manual returns carry their profit
        // on their own dated adjustment sale, so this stays 0 for those.
        [Column(TypeName = "decimal(18,2)")]
        public decimal ProfitImpact { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation
        [ForeignKey(nameof(SaleId))]
        public Sale? Sale { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
        public ICollection<ReplacementItem> ReplacementItems { get; set; } = new List<ReplacementItem>();
    }

    public class ReturnItem
    {
        [Key]
        public int Id { get; set; }

        public int ReturnId { get; set; }
        public int ProductId { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // Navigation
        [ForeignKey(nameof(ReturnId))]
        public Return? Return { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }
    }

    public class ReplacementItem
    {
        [Key]
        public int Id { get; set; }

        public int ReturnId { get; set; }
        public int ProductId { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // Navigation
        [ForeignKey(nameof(ReturnId))]
        public Return? Return { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }
    }
}
