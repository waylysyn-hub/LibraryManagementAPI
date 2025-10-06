using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Domain.DTOs
{
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
        [Required, MaxLength(150)]
        public string Name { get; set; } = default!;

        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = default!;

        [Phone, MaxLength(30)]
        public string? Phone { get; set; }
    }

    // تحديث إداري
    public class MemberAdminUpdateDto
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = default!;

        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = default!;

        [Phone, MaxLength(30)]
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

        [Range(1, int.MaxValue)]
        [DefaultValue(1)]
        public int Page { get; set; } = 1;

        [Range(1, 200)]
        [DefaultValue(50)]
        public int PageSize { get; set; } = 50;
    }
}
