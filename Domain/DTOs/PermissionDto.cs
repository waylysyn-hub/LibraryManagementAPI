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
        [Required(ErrorMessage = "اسم الصلاحية مطلوب")]
        [StringLength(100, ErrorMessage = "اسم الصلاحية يجب أن لا يتجاوز 100 محرف")]
        public string Name { get; set; } = null!;
    }

    public class PermissionUpdateDto
    {
        [Required(ErrorMessage = "اسم الصلاحية مطلوب")]
        [StringLength(100, ErrorMessage = "اسم الصلاحية يجب أن لا يتجاوز 100 محرف")]
        public string Name { get; set; } = null!;
    }
}
