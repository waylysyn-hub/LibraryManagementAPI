using Data;
using Domain.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Data.Services
{
    public class MemberService
    {
        private readonly BankDbContext _context;

        private const int DefaultPage = 1;
        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 200;

        public MemberService(BankDbContext context) => _context = context;

        // للواجهة القديمة: إبقاء التوقيع البسيط
        public async Task<List<MemberDto>> GetAllAsync(string? name, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            var qp = new MemberQueryParams { Name = name, Page = page, PageSize = pageSize };
            var (items, _) = await GetPagedAsync(qp, ct);
            return items;
        }

        public async Task<(List<MemberDto> items, int total)> GetPagedAsync(MemberQueryParams qp, CancellationToken ct = default)
        {
            var page = qp.Page < 1 ? DefaultPage : qp.Page;
            var pageSize = qp.PageSize <= 0 ? DefaultPageSize : (qp.PageSize > MaxPageSize ? MaxPageSize : qp.PageSize);

            var query = _context.Members.AsNoTracking().AsQueryable();

            // فلترة عامة Q
            if (!string.IsNullOrWhiteSpace(qp.Q))
            {
                var q = qp.Q.Trim().ToLower();
                query = query.Where(m =>
                    m.Name.ToLower().Contains(q) ||
                    m.Email.ToLower().Contains(q) ||
                    (m.Phone != null && m.Phone.ToLower().Contains(q))
                );
            }

            // فلترة بالاسم/الإيميل/الهاتف
            if (!string.IsNullOrWhiteSpace(qp.Name))
            {
                var name = qp.Name.Trim();
                query = query.Where(m => EF.Functions.Like(m.Name, $"%{name}%"));
            }

            if (!string.IsNullOrWhiteSpace(qp.Email))
            {
                var email = qp.Email.Trim().ToLowerInvariant();
                query = query.Where(m => m.Email.ToLower() == email);
            }

            if (!string.IsNullOrWhiteSpace(qp.Phone))
            {
                var phone = qp.Phone.Trim();
                query = query.Where(m => m.Phone != null && EF.Functions.Like(m.Phone, $"%{phone}%"));
            }

            // فلترة بنطاق تاريخ التسجيل
            if (qp.RegisteredFrom.HasValue)
            {
                var from = DateTime.SpecifyKind(qp.RegisteredFrom.Value, DateTimeKind.Utc);
                query = query.Where(m => m.RegisteredAt >= from);
            }
            if (qp.RegisteredTo.HasValue)
            {
                var to = DateTime.SpecifyKind(qp.RegisteredTo.Value, DateTimeKind.Utc);
                query = query.Where(m => m.RegisteredAt <= to);
            }

            var total = await query.CountAsync(ct);

            // فرز
            var asc = qp.SortDir == SortDirection.asc;
            query = qp.SortBy switch
            {
                MemberSortBy.Name => asc ? query.OrderBy(m => m.Name) : query.OrderByDescending(m => m.Name),
                MemberSortBy.Email => asc ? query.OrderBy(m => m.Email) : query.OrderByDescending(m => m.Email),
                MemberSortBy.RegisteredAt => asc ? query.OrderBy(m => m.RegisteredAt) : query.OrderByDescending(m => m.RegisteredAt),
                _ => asc ? query.OrderBy(m => m.Id) : query.OrderByDescending(m => m.Id)
            };

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MemberDto
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    Name = m.Name,
                    Email = m.Email,
                    Phone = m.Phone,
                    RegisteredAt = m.RegisteredAt
                })
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<MemberDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Members.AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => new MemberDto
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    Name = m.Name,
                    Email = m.Email,
                    Phone = m.Phone,
                    RegisteredAt = m.RegisteredAt
                })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<MemberDto?> GetByUserIdAsync(int userId, CancellationToken ct = default)
        {
            return await _context.Members.AsNoTracking()
                .Where(m => m.UserId == userId)
                .Select(m => new MemberDto
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    Name = m.Name,
                    Email = m.Email,
                    Phone = m.Phone,
                    RegisteredAt = m.RegisteredAt
                })
                .FirstOrDefaultAsync(ct);
        }

        // تحديث ذاتي (العضو يعدّل ملفه)
        public async Task<bool> UpdateSelfAsync(int userId, MemberSelfUpdateDto dto, CancellationToken ct = default)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.UserId == userId, ct);
            if (member == null) return false;

            var newName = dto.Name.Trim();
            var newEmail = dto.Email.Trim().ToLowerInvariant();
            var newPhone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();

            if (!string.Equals(member.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                var emailTaken = await _context.Members
                    .AsNoTracking()
                    .AnyAsync(m => m.Email.ToLower() == newEmail && m.UserId != userId, ct);
                if (emailTaken) throw new DuplicateNameException($"Email '{dto.Email}' is already in use.");

                member.Email = newEmail;

                // (اختياري) مزامنة بريد المستخدم
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
                if (user != null) user.Email = newEmail;
            }

            member.Name = newName;
            member.Phone = newPhone;

            await _context.SaveChangesAsync(ct);
            return true;
        }

        // تحديث إداري
        public async Task<bool> AdminUpdateAsync(int id, MemberAdminUpdateDto dto, CancellationToken ct = default)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (member == null) return false;

            var newName = dto.Name.Trim();
            var newEmail = dto.Email.Trim().ToLowerInvariant();
            var newPhone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();

            if (!string.Equals(member.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                var emailTaken = await _context.Members
                    .AsNoTracking()
                    .AnyAsync(m => m.Email.ToLower() == newEmail && m.Id != id, ct);
                if (emailTaken) throw new DuplicateNameException($"Email '{dto.Email}' is already in use.");

                member.Email = newEmail;

                // (اختياري) مزامنة بريد المستخدم المرتبط
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == member.UserId, ct);
                if (user != null) user.Email = newEmail;
            }

            member.Name = newName;
            member.Phone = newPhone;

            await _context.SaveChangesAsync(ct);
            return true;
        }

        // حذف إداري
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var member = await _context.Members
                .Include(m => m.BorrowRecords)
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            if (member == null) return false;

            if (member.BorrowRecords?.Count > 0)
                throw new InvalidOperationException("لا يمكن حذف عضو لديه سجلات استعارة.");

            _context.Members.Remove(member);
            await _context.SaveChangesAsync(ct);
            return true;
        }

        // تصدير
        public async Task<List<MemberDto>> GetForExportAsync(MemberQueryParams qp, int maxRows = 10000, CancellationToken ct = default)
        {
            var query = _context.Members.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(qp.Q))
            {
                var q = qp.Q.Trim().ToLower();
                query = query.Where(m =>
                    m.Name.ToLower().Contains(q) ||
                    m.Email.ToLower().Contains(q) ||
                    (m.Phone != null && m.Phone.ToLower().Contains(q))
                );
            }
            if (!string.IsNullOrWhiteSpace(qp.Name))
                query = query.Where(m => EF.Functions.Like(m.Name, $"%{qp.Name.Trim()}%"));

            if (!string.IsNullOrWhiteSpace(qp.Email))
            {
                var email = qp.Email.Trim().ToLowerInvariant();
                query = query.Where(m => m.Email.ToLower() == email);
            }

            if (!string.IsNullOrWhiteSpace(qp.Phone))
            {
                var phone = qp.Phone.Trim();
                query = query.Where(m => m.Phone != null && EF.Functions.Like(m.Phone, $"%{phone}%"));
            }

            if (qp.RegisteredFrom.HasValue)
            {
                var from = DateTime.SpecifyKind(qp.RegisteredFrom.Value, DateTimeKind.Utc);
                query = query.Where(m => m.RegisteredAt >= from);
            }
            if (qp.RegisteredTo.HasValue)
            {
                var to = DateTime.SpecifyKind(qp.RegisteredTo.Value, DateTimeKind.Utc);
                query = query.Where(m => m.RegisteredAt <= to);
            }

            var asc = qp.SortDir == SortDirection.asc;
            query = qp.SortBy switch
            {
                MemberSortBy.Name => asc ? query.OrderBy(m => m.Name) : query.OrderByDescending(m => m.Name),
                MemberSortBy.Email => asc ? query.OrderBy(m => m.Email) : query.OrderByDescending(m => m.Email),
                MemberSortBy.RegisteredAt => asc ? query.OrderBy(m => m.RegisteredAt) : query.OrderByDescending(m => m.RegisteredAt),
                _ => asc ? query.OrderBy(m => m.Id) : query.OrderByDescending(m => m.Id)
            };

            return await query
                .Take(maxRows)
                .Select(m => new MemberDto
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    Name = m.Name,
                    Email = m.Email,
                    Phone = m.Phone,
                    RegisteredAt = m.RegisteredAt
                })
                .ToListAsync(ct);
        }
    }
}
