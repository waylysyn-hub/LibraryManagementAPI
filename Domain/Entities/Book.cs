// Domain.Entities/Book.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    public class Book
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required, MaxLength(150)]
        public string Author { get; set; } = null!;

        // جديد: التصنيف
        [Required, MaxLength(100)]
        public string Category { get; set; } = null!;

        // جديد: سنة النشر
        [Range(1500, 3000)]
        public int Year { get; set; }

        // جديد: عدد النسخ الكليّة
        [Range(0, int.MaxValue)]
        public int CopiesCount { get; set; }

        // موجود لديك – نتركه اختياريًا (مفيد للفهرسة)
        [MaxLength(20)]
        public string? ISBN { get; set; }

        public ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();

        // 💡 للعرض فقط: عدد النسخ المتاحة حاليًا (غير مخزّن)
        [NotMapped]
        public int AvailableCopies =>
            CopiesCount - BorrowRecords.Count(br => br.ReturnedDate == null);
    }
}
