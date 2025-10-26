using Swashbuckle.AspNetCore.Annotations;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Domain.DTOs
{
    public static class Regexes
    {
        public const string SyrianPhone = @"^(?:09\d{9}|\+963\d{9})$"; // Regex للهاتف السوري
    }

    // نموذج تسجيل مستخدم
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

        // ✅ جديد: رقم الهاتف (اختياري)
        [MaxLength(14)]
        [RegularExpression(Regexes.SyrianPhone, ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام.")]
        public string? Phone { get; set; }
    }

    // تعريف الأدوار في النظام
    [JsonConverter(typeof(JsonStringEnumConverter))]
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

    // نموذج إنشاء مستخدم من قبل المسؤول
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

        [Required]
        public AdminRoleKey Role { get; set; } = AdminRoleKey.Employee;

        [MaxLength(150)]
        public string? Name { get; set; }

        // ✅ جديد (اختياري) رقم الهاتف
        [MaxLength(14)]
        [RegularExpression(Regexes.SyrianPhone, ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام.")]
        public string? Phone { get; set; }
    }

    // تحديث معلومات المستخدم
    public class UserUpdateDto
    {
        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
        public string Email { get; set; } = null!;

        [MaxLength(14)]
        [RegularExpression(Regexes.SyrianPhone, ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام.")]
        public string? Phone { get; set; }
    }

    // تغيير كلمة مرور المستخدم
    public class UpdatePasswordDto
    {
        [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة.")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة.")]
        [MinLength(6, ErrorMessage = "كلمة المرور الجديدة يجب ألا تقل عن 6 محارف.")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب.")]
        [Compare("NewPassword", ErrorMessage = "تأكيد كلمة المرور غير متطابق.")]
        public string ConfirmNewPassword { get; set; }
    }

    // معلومات المستخدم (للقراءة فقط)
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int RoleId { get; set; }
        public string? Phone { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public sealed class UsersQuery
    {
        public string? Name { get; set; }     // qName()
        public string? Email { get; set; }    // qEmail()
        public string? Phone { get; set; }    // qPhone()
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public string SortBy { get; set; } = "CreatedAt"; // أو "Id"/"Username"/"Email"
        public string SortDir { get; set; } = "Desc";     // "Asc"/"Desc"
    }
    
}
