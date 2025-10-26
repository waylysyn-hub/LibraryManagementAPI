using System.Data;
using Data;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Data.Services
{
    public class BorrowRecordService
    {
        private readonly BankDbContext _context;
        private readonly ILogger<BorrowRecordService> _logger;

        public BorrowRecordService(BankDbContext context, ILogger<BorrowRecordService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // باجينيشن
        public async Task<(List<BorrowRecordDto> items, int total)> GetPagedAsync(
            int? memberId, int? bookId, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            var query = _context.BorrowRecords.AsNoTracking().AsQueryable();

            if (memberId.HasValue) query = query.Where(br => br.MemberId == memberId.Value);
            if (bookId.HasValue) query = query.Where(br => br.BookId == bookId.Value);

            var total = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(br => br.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(br => new BorrowRecordDto
                {
                    Id = br.Id,
                    MemberId = br.MemberId,
                    BookId = br.BookId,
                    BorrowedDate = br.BorrowedDate,
                    DueDate = br.DueDate,
                    ReturnedDate = br.ReturnedDate
                })
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<BorrowRecordDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.BorrowRecords.AsNoTracking()
                .Where(br => br.Id == id)
                .Select(br => new BorrowRecordDto
                {
                    Id = br.Id,
                    MemberId = br.MemberId,
                    BookId = br.BookId,
                    BorrowedDate = br.BorrowedDate,
                    DueDate = br.DueDate,
                    ReturnedDate = br.ReturnedDate
                })
                .FirstOrDefaultAsync(ct);
        }

        // تصدير
        public async Task<List<BorrowRecordExportRow>> GetForExportAsync(
            int? memberId, int? bookId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            var query = _context.BorrowRecords.AsNoTracking().AsQueryable();
            if (memberId.HasValue) query = query.Where(br => br.MemberId == memberId.Value);
            if (bookId.HasValue) query = query.Where(br => br.BookId == bookId.Value);

            var rows = await query
                .OrderByDescending(br => br.Id)
                .Select(br => new BorrowRecordExportRow
                {
                    Id = br.Id,
                    MemberId = br.MemberId,
                    MemberName = br.Member.Name,
                    BookId = br.BookId,
                    BookTitle = br.Book.Title,
                    BorrowedDate = br.BorrowedDate,
                    DueDate = br.DueDate,
                    ReturnedDate = br.ReturnedDate
                })
                .ToListAsync(ct);

            foreach (var r in rows)
            {
                var isReturned = r.ReturnedDate.HasValue;
                var effectiveEnd = r.ReturnedDate ?? now;

                if (!isReturned && now <= r.DueDate)
                    r.Status = "نشط";
                else if (!isReturned && now > r.DueDate)
                    r.Status = "متأخر";
                else
                    r.Status = effectiveEnd > r.DueDate ? "مُعاد (متأخر)" : "مُعاد";

                r.OverdueDays = effectiveEnd > r.DueDate
                    ? (int)Math.Floor((effectiveEnd - r.DueDate).TotalDays)
                    : 0;
            }

            return rows;
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

        // إنشاء
        public async Task<BorrowRecord> AddAsync(BorrowRecordCreateDto dto, CancellationToken ct = default)
        {
            if (dto.DurationDays <= 0 || dto.DurationDays > 365)
                throw new ArgumentException("عدد أيام الإعارة يجب أن يكون بين 1 و 365.");

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var bookInfo = await _context.Books
                .Where(b => b.Id == dto.BookId)
                .Select(b => new
                {
                    b.Id,
                    b.CopiesCount,
                    Active = b.BorrowRecords.Count(br => br.ReturnedDate == null)
                })
                .FirstOrDefaultAsync(ct);

            if (bookInfo is null)
                throw new ArgumentException($"الكتاب ذو المعرّف {dto.BookId} غير موجود.");

            var memberExists = await _context.Members.AnyAsync(m => m.Id == dto.MemberId, ct);
            if (!memberExists)
                throw new ArgumentException($"العضو ذو المعرّف {dto.MemberId} غير موجود.");

            var duplicateActive = await _context.BorrowRecords
                .AnyAsync(br => br.MemberId == dto.MemberId
                             && br.BookId == dto.BookId
                             && br.ReturnedDate == null, ct);
            if (duplicateActive)
                throw new InvalidOperationException("لدى العضو استعارة نشطة لهذا الكتاب بالفعل.");

            var available = bookInfo.CopiesCount - bookInfo.Active;
            if (available <= 0)
                throw new InvalidOperationException("لا توجد نسخ متاحة من هذا الكتاب حالياً.");

            var now = DateTime.UtcNow;
            var record = new BorrowRecord
            {
                MemberId = dto.MemberId,
                BookId = dto.BookId,
                BorrowedDate = now,
                DueDate = now.AddDays(dto.DurationDays)
            };

            _context.BorrowRecords.Add(record);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Borrow record created: {@BorrowRecord}", record);
            return record;
        }

        public async Task<bool> UpdateAsync(int id, BorrowRecordUpdateDto dto, CancellationToken ct = default)
        {
            if (dto.DurationDays <= 0 || dto.DurationDays > 365)
                throw new ArgumentException("عدد أيام الإعارة يجب أن يكون بين 1 و 365.");

            var record = await _context.BorrowRecords.FindAsync(new object?[] { id }, ct);
            if (record == null) return false;

            var bookExists = await _context.Books.AnyAsync(b => b.Id == dto.BookId, ct);
            if (!bookExists)
                throw new ArgumentException($"الكتاب ذو المعرّف {dto.BookId} غير موجود.");

            var memberExists = await _context.Members.AnyAsync(m => m.Id == dto.MemberId, ct);
            if (!memberExists)
                throw new ArgumentException($"العضو ذو المعرّف {dto.MemberId} غير موجود.");

            var isAlreadyBorrowedBySameMember = await _context.BorrowRecords
                .AnyAsync(br => br.BookId == dto.BookId && br.MemberId == dto.MemberId && br.Id != id && br.ReturnedDate == null, ct);
            if (isAlreadyBorrowedBySameMember)
                throw new ArgumentException("هذا الكتاب مُستعار مسبقاً من نفس العضو.");

            // 🟢 فحص التوفر الحقيقي إذا تم تغيير الكتاب
            if (dto.BookId != record.BookId)
            {
                var target = await _context.Books
                    .Where(b => b.Id == dto.BookId)
                    .Select(b => new
                    {
                        b.CopiesCount,
                        Active = b.BorrowRecords.Count(br => br.ReturnedDate == null)
                    })
                    .FirstOrDefaultAsync(ct);

                if (target == null || (target.CopiesCount - target.Active) <= 0)
                    throw new InvalidOperationException("لا توجد نسخ متاحة من الكتاب المستهدف حالياً.");
            }

            var now = DateTime.UtcNow;
            record.MemberId = dto.MemberId;
            record.BookId = dto.BookId;
            record.BorrowedDate = now;
            record.DueDate = now.AddDays(dto.DurationDays);

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Borrow record updated: {@BorrowRecord}", record);

            return true;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var record = await _context.BorrowRecords.FindAsync(new object?[] { id }, ct);
            if (record == null) return false;

            _context.BorrowRecords.Remove(record);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Borrow record deleted: {@BorrowRecord}", record);
            return true;
        }

        public async Task<bool> ReturnAsync(int id, CancellationToken ct = default)
        {
            var record = await _context.BorrowRecords.FindAsync(new object?[] { id }, ct);
            if (record == null) return false;

            if (record.ReturnedDate != null)
                throw new InvalidOperationException("تم إرجاع هذا السجل مسبقاً.");

            record.ReturnedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Borrow record returned: {@BorrowRecord}", record);
            return true;
        }
    }
}
