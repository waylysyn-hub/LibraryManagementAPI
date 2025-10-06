// Domain.Entities/BorrowRecord.cs
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class BorrowRecord
    {
        public int Id { get; set; }

        [Required]
        public int BookId { get; set; }

        [Required]
        public int MemberId { get; set; }

        // تاريخ الاستعارة
        public DateTime BorrowedDate { get; set; }

        // تاريخ الاستحقاق (يبقى مفيد حتى لو نحسبه من المدة)
        public DateTime DueDate { get; set; }

        // ✅ جديد: تاريخ الإرجاع الفعلي (Nullable)
        public DateTime? ReturnedDate { get; set; }

        public Book Book { get; set; } = null!;
        public Member Member { get; set; } = null!;
    }
}
