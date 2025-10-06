using Swashbuckle.AspNetCore.Annotations;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;


namespace Domain.DTOs
{



    public class UserRegisterDto
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = default!;

        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required, Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
    [JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum AdminRoleKey
    {
        Admin = 1,
        Employee = 2
    }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoleKey
    {
        Admin = 1,
        Employee = 2,
        Member = 3
    }

    public class AdminCreateUserDto
    {
        [Required, MaxLength(100)]
        public string Username { get; set; } = default!;

        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = default!;

        [Required, MinLength(6)]
        public string Password { get; set; } = default!;

        [Required, Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = default!;

        [Required] // 👈 Dropdown: Admin | Employee
        public AdminRoleKey Role { get; set; } = AdminRoleKey.Employee;

        [MaxLength(150)]
        public string? Name { get; set; } // للعرض فقط، لن ننشئ Member هنا
    }
    // ============================================================
    // Update User Info DTO
    // ============================================================
    public class UserUpdateDto
    {
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = null!;
    }

    // ============================================================
    // Change Password DTO
    // ============================================================
    public class UpdatePasswordDto
    {
        [Required(ErrorMessage = "Current password is required")]
        public string CurrentPassword { get; set; } = null!;

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string NewPassword { get; set; } = null!;

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("NewPassword", ErrorMessage = "New password and confirmation do not match")]
        public string ConfirmPassword { get; set; } = null!;
    }

    // ============================================================
    // User DTO (Read-only)
    // ============================================================
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int RoleId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
