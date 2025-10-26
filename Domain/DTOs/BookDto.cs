using Domain.Entities;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Domain.DTOs
{
    public class BookDto
    {
        public int Id { get; set; }

        public required string Title { get; set; }
        public required string Author { get; set; }

        [Required, MaxLength(32)]
        [RegularExpression(@"^[\d\s\-xX]+$", ErrorMessage = "يجب أن يحتوي ISBN على أرقام أو فراغات أو شرطات فقط.")]
        public string ISBN { get; set; } = default!;

        public required string Category { get; set; }

        public int Year { get; set; }
        public int CopiesCount { get; set; }

        // عرض عدد الاستعارات النشطة والمتاح
        public int ActiveBorrowCount { get; set; }    // ReturnedDate == null
        public int AvailableCopies { get; set; }

        // للعرض الاختياري
        public int? BorrowCount { get; set; }
    }

    public enum BookSortBy
    {
        Id,
        Title,
        Author,
        ISBN,
        Category,
        Year,
        CopiesCount
    }

    public class BookQueryParams
    {
        public string? Q { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Category { get; set; }
        public string? Isbn { get; set; }

        [DefaultValue(false)]
        public bool IsbnStartsWith { get; set; } = false;

        // فلاتر على السنة والنسخ
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public int? MinCopies { get; set; }
        public int? MaxCopies { get; set; }

        [DefaultValue(BookSortBy.Id)]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public BookSortBy SortBy { get; set; } = BookSortBy.Id;

        [DefaultValue(SortDirection.asc)]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SortDirection SortDir { get; set; } = SortDirection.asc;

        [DefaultValue(1)]
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [DefaultValue(50)]
        [Range(1, 200)]
        public int PageSize { get; set; } = 50;

        [DefaultValue(false)]
        public bool IncludeBorrowCount { get; set; } = false;
    }

    public class BookCreateDto
    {
        [Required(ErrorMessage = "العنوان مطلوب"), MaxLength(200)]
        public string Title { get; set; } = default!;

        [Required(ErrorMessage = "المؤلف مطلوب"), MaxLength(150)]
        public string Author { get; set; } = default!;

        [Required(ErrorMessage = "التصنيف مطلوب"), MaxLength(100)]
        public string Category { get; set; } = default!;

        [NotInFutureYear(ErrorMessage = "السنة يجب أن تكون بين 1500 والسنة الحالية.")]
        public int Year { get; set; }

        [Range(0, 1000, ErrorMessage = "عدد النسخ يجب أن يكون بين 0 و 1000.")]
        public int CopiesCount { get; set; }

        [Required(ErrorMessage = "ISBN مطلوب"), MaxLength(32)]
        [RegularExpression(@"^[\d\s\-xX]+$", ErrorMessage = "يجب أن يحتوي ISBN على أرقام أو فراغات أو شرطات فقط.")]
        public string ISBN { get; set; } = default!;
    }

    public class BookUpdateDto : BookCreateDto { }
}
