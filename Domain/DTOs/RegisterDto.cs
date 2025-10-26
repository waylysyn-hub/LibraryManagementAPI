using System.ComponentModel.DataAnnotations;

namespace Domain.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [MinLength(6, ErrorMessage = "كلمة المرور يجب ألا تقل عن 6 محارف")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare("Password", ErrorMessage = "كلمتا المرور غير متطابقتين")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "الدور مطلوب")]
        public string RoleId { get; set; } = "Student"; // أبقيتها كما هي إن كانت مستخدمة لديكم
    }
}
