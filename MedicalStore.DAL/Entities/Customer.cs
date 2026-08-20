using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalStore.DAL.Entities
{
    public class Customer
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } // Positive = customer owes us

        // Starting balance when the customer was first added to the system
        // (e.g. an existing debtor being onboarded). Included in Balance and ledgers.
        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningBalance { get; set; }

        public bool IsWalkIn { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
        public ICollection<Return> Returns { get; set; } = new List<Return>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
