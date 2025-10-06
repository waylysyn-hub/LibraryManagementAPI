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
        [RegularExpression(@"^[\d\s\-xX]+$", ErrorMessage = "ISBN must contain digits, spaces or hyphens only.")]
        public string ISBN { get; set; } = default!; public required string Category { get; set; }
        public int Year { get; set; }
        public int CopiesCount { get; set; }
        // جديد: نعرض عدد الاستعارات النشطة والمتاح
        public int ActiveBorrowCount { get; set; }    // استعارات غير مُعادة (ReturnedDate == null)
        public int AvailableCopies { get; set; }
        public int? BorrowCount { get; set; } // اختياري للعرض
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
        [Required, MaxLength(200)]
        public string Title { get; set; } = default!;

        [Required, MaxLength(150)]
        public string Author { get; set; } = default!;

        [Required, MaxLength(100)]
        public string Category { get; set; } = default!;

        [NotInFutureYear(ErrorMessage = "Year must be between 1500 and current year.")]
        public int Year { get; set; }

        [Range(0, 1000, ErrorMessage = "CopiesCount must be between 0 and 1000.")]
        public int CopiesCount { get; set; }

        // نقبل إدخال متنوّع، التحقق النهائي بعد التطبيع بالخدمة
        [Required, MaxLength(32)]
        [RegularExpression(@"^[\d\s\-xX]+$", ErrorMessage = "ISBN must contain digits, spaces or hyphens only.")]
        public string ISBN { get; set; } = default!;
    }

    public class BookUpdateDto : BookCreateDto { }

}
