using System.ComponentModel.DataAnnotations;

namespace MedicalStore.DAL.Entities
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public int UserId { get; set; }

        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        // What happened, e.g. "Customer Payment", "Price Change", "Purchase Cancelled",
        // "Opening Balance", "Supplier Return".
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        // The affected entity type + id, e.g. "Product", "Customer".
        [MaxLength(50)]
        public string Entity { get; set; } = string.Empty;

        public int EntityId { get; set; }

        // Human-readable detail, typically "old → new" or an amount.
        [MaxLength(1000)]
        public string Details { get; set; } = string.Empty;
    }
}
