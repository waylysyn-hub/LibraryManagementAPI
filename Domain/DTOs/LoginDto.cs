using System.ComponentModel.DataAnnotations;

namespace Domain.DTOs
{
    // DTO صغير للبريد وكلمة السر
   
    public class LoginDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")] // ✅ يتحقق إنو إيميل صحيح
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }
        public class LoginResultDto
        {
            public string Token { get; set; } = string.Empty;
            public string RoleName { get; set; } = string.Empty; // "Admin", "Teacher", "Student"
            public List<string> Permissions { get; set; } = new();
        }

}

