using System.ComponentModel.DataAnnotations;

namespace Domain.DTOs
{
    public class BorrowRecordCreateDto
    {
        [Required(ErrorMessage = "معرّف العضو مطلوب")]
        public int MemberId { get; set; }

        [Required(ErrorMessage = "معرّف الكتاب مطلوب")]
        public int BookId { get; set; }

        [Required(ErrorMessage = "مدة الإعارة مطلوبة")]
        [Range(1, 365, ErrorMessage = "المدة يجب أن تكون بين 1 و 365 يومًا")]
        public int DurationDays { get; set; }
    }

    public class BorrowRecordUpdateDto
    {
        [Required(ErrorMessage = "معرّف العضو مطلوب")]
        public int MemberId { get; set; }

        [Required(ErrorMessage = "معرّف الكتاب مطلوب")]
        public int BookId { get; set; }

        [Required(ErrorMessage = "مدة الإعارة مطلوبة")]
        [Range(1, 365, ErrorMessage = "المدة يجب أن تكون بين 1 و 365 يومًا")]
        public int DurationDays { get; set; }
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
        public DateTime? ReturnedDate { get; set; }
    }
}
