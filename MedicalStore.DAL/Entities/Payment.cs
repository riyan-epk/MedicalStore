using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalStore.DAL.Entities
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        public int CustomerId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation
        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }
    }
}
