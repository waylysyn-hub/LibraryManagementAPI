// Domain.Entities/Member.cs
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class Member
    {
        public int Id { get; set; }

        // علاقة 1-1 مع User (كما هو عندك)
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        // جديد: رقم الهاتف
        [Phone, MaxLength(30)]
        public string? Phone { get; set; }

        // جديد: تاريخ التسجيل
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        public ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();
    }
}
