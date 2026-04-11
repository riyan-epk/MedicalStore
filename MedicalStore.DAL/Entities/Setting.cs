using System.ComponentModel.DataAnnotations;

namespace MedicalStore.DAL.Entities
{
    public class Setting
    {
        [Key]
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
