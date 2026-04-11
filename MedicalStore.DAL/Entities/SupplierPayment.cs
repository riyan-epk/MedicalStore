using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalStore.DAL.Entities
{
    public class SupplierPayment
    {
        [Key]
        public int Id { get; set; }

        public int SupplierId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "Cash";

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation
        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }
    }
}
