using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace BulkyWebRazor.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [DisplayName("Category Name")]
        [MaxLength(30)]
        public string Name { get; set; } = string.Empty;

        [Range(1, 100, ErrorMessage = "The field Display Order must be between 1 and 100!")]
        [DisplayName("Display Order")]
        public int DisplayOrder { get; set; }
    }
}
