using System;
using System.ComponentModel.DataAnnotations;

namespace Domain.DTOs
{
    public class BorrowRecordCreateDto
    {
        [Required(ErrorMessage = "MemberId is required.")]
        public int MemberId { get; set; }

        [Required(ErrorMessage = "BookId is required.")]
        public int BookId { get; set; }

        [Required(ErrorMessage = "DurationDays is required.")]
        [Range(1, 365, ErrorMessage = "Duration must be between 1 and 365 days.")]
        public int DurationDays { get; set; } // مدة الإعارة بالأيام
    }

    public class BorrowRecordUpdateDto
    {
        [Required(ErrorMessage = "MemberId is required.")]
        public int MemberId { get; set; }

        [Required(ErrorMessage = "BookId is required.")]
        public int BookId { get; set; }

        [Required]
        public int DurationDays { get; set; } // نستخدم DurationDays بدل إدخال تواريخ يدوياً
    }
            public class BorrowRecordExportRow
        {
            public int Id { get; set; }
            public int MemberId { get; set; }
            public string? MemberName { get; set; }
            public int BookId { get; set; }
            public string? BookTitle { get; set; }
            public DateTime BorrowedDate { get; set; }
            public DateTime DueDate { get; set; }
            public DateTime? ReturnedDate { get; set; }

            public string Status { get; set; } = "";
            public int OverdueDays { get; set; }
        }
    public class BorrowRecordDto
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public int BookId { get; set; }
        public DateTime BorrowedDate { get; set; }
        public DateTime DueDate { get; set; }
        // جديد: يظهر حالة الإرجاع
        public DateTime? ReturnedDate { get; set; }
    }
}
