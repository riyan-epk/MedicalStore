using System.ComponentModel.DataAnnotations;
using MedicalStore.Common.Enums;

namespace MedicalStore.DAL.Entities
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        public UserRole Role { get; set; }

        public Permission Permissions { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}
