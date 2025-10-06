using System.ComponentModel.DataAnnotations;

namespace Domain.DTOs
{
    public class PermissionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    public class PermissionCreateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;
    }

    public class PermissionUpdateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;
    }
}
