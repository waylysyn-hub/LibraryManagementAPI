using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Domain.DTOs
{
    public static class MemberRegexes
    {
        // 09 + 9 أرقام أو +963 + 9 أرقام
        public const string SyrianPhone = @"^(?:09\d{9}|\+963\d{9})$";
    }

    public class MemberDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }
        public string? Phone { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    // تحديث ذاتي بواسطة العضو
    public class MemberSelfUpdateDto
    {
        [Required(ErrorMessage = "الاسم مطلوب"), MaxLength(150, ErrorMessage = "الاسم يجب ألا يتجاوز 150 محرفاً")]
        public string Name { get; set; } = default!;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
        [MaxLength(200, ErrorMessage = "البريد الإلكتروني يجب ألا يتجاوز 200 محرف")]
        public string Email { get; set; } = default!;

        [MaxLength(14, ErrorMessage = "رقم الهاتف يجب ألا يتجاوز 14 محرفاً")]
        [RegularExpression(MemberRegexes.SyrianPhone, ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام.")]
        public string? Phone { get; set; }
    }

    // تحديث إداري
    public class MemberAdminUpdateDto
    {
        [Required(ErrorMessage = "الاسم مطلوب"), MaxLength(150, ErrorMessage = "الاسم يجب ألا يتجاوز 150 محرفاً")]
        public string Name { get; set; } = default!;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
        [MaxLength(200, ErrorMessage = "البريد الإلكتروني يجب ألا يتجاوز 200 محرف")]
        public string Email { get; set; } = default!;

        [MaxLength(14, ErrorMessage = "رقم الهاتف يجب ألا يتجاوز 14 محرفاً")]
        [RegularExpression(MemberRegexes.SyrianPhone, ErrorMessage = "رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام.")]
        public string? Phone { get; set; }
    }

    public enum MemberSortBy
    {
        Id,
        Name,
        Email,
        RegisteredAt
    }

    public enum SortDirection
    {
        asc,
        desc
    }

    public class MemberQueryParams
    {
        public string? Q { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }

        public DateTime? RegisteredFrom { get; set; }
        public DateTime? RegisteredTo { get; set; }

        [DefaultValue(MemberSortBy.Id)]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MemberSortBy SortBy { get; set; } = MemberSortBy.Id;

        [DefaultValue(SortDirection.asc)]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SortDirection SortDir { get; set; } = SortDirection.asc;

        [Range(1, int.MaxValue, ErrorMessage = "رقم الصفحة يجب أن يكون 1 على الأقل")]
        [DefaultValue(1)]
        public int Page { get; set; } = 1;

        [Range(1, 200, ErrorMessage = "حجم الصفحة يجب أن يكون بين 1 و 200")]
        [DefaultValue(50)]
        public int PageSize { get; set; } = 50;
    }
}
