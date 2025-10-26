using Data;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Data.Services
{
    public class BookService
    {
        private const int DefaultPage = 1;
        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 200;

        private readonly BankDbContext _context;

        public BookService(BankDbContext context) => _context = context;

        public async Task<(List<BookDto> items, int total)> GetPagedAsync(BookQueryParams qp, CancellationToken ct = default)
        {
            var page = qp.Page < 1 ? DefaultPage : qp.Page;
            var pageSize = qp.PageSize <= 0 ? DefaultPageSize :
                           (qp.PageSize > MaxPageSize ? MaxPageSize : qp.PageSize);

            var query = _context.Books.AsNoTracking().AsQueryable();

            // بحث عام
            if (!string.IsNullOrWhiteSpace(qp.Q))
            {
                var q = qp.Q.Trim();
                query = query.Where(b =>
                    EF.Functions.Like(b.Title, $"%{q}%") ||
                    EF.Functions.Like(b.Author, $"%{q}%") ||
                    EF.Functions.Like(b.Category, $"%{q}%") ||
                    EF.Functions.Like(b.ISBN, $"%{q}%"));
            }

            if (!string.IsNullOrWhiteSpace(qp.Title))
                query = query.Where(b => EF.Functions.Like(b.Title, $"%{qp.Title!.Trim()}%"));

            if (!string.IsNullOrWhiteSpace(qp.Author))
                query = query.Where(b => EF.Functions.Like(b.Author, $"%{qp.Author!.Trim()}%"));

            if (!string.IsNullOrWhiteSpace(qp.Category))
                query = query.Where(b => EF.Functions.Like(b.Category, $"%{qp.Category!.Trim()}%"));

            // فلترة ISBN (REPLACE + LOWER)
            if (!string.IsNullOrWhiteSpace(qp.Isbn))
            {
                var i = NormalizeIsbnInput(qp.Isbn.Trim());
                if (qp.IsbnStartsWith)
                {
                    query = query.Where(b =>
                        ((b.ISBN ?? "").Replace("-", "").Replace(" ", "").ToLower()).StartsWith(i));
                }
                else
                {
                    query = query.Where(b =>
                        ((b.ISBN ?? "").Replace("-", "").Replace(" ", "").ToLower()) == i);
                }
            }

            // فلترة سنة ونسخ
            if (qp.YearFrom.HasValue) query = query.Where(b => b.Year >= qp.YearFrom.Value);
            if (qp.YearTo.HasValue) query = query.Where(b => b.Year <= qp.YearTo.Value);
            if (qp.MinCopies.HasValue) query = query.Where(b => b.CopiesCount >= qp.MinCopies.Value);
            if (qp.MaxCopies.HasValue) query = query.Where(b => b.CopiesCount <= qp.MaxCopies.Value);

            var total = await query.CountAsync(ct);

            // فرز
            var asc = qp.SortDir == SortDirection.asc;
            query = qp.SortBy switch
            {
                BookSortBy.Title => asc ? query.OrderBy(b => b.Title) : query.OrderByDescending(b => b.Title),
                BookSortBy.Author => asc ? query.OrderBy(b => b.Author) : query.OrderByDescending(b => b.Author),
                BookSortBy.ISBN => asc ? query.OrderBy(b => b.ISBN) : query.OrderByDescending(b => b.ISBN),
                BookSortBy.Category => asc ? query.OrderBy(b => b.Category) : query.OrderByDescending(b => b.Category),
                BookSortBy.Year => asc ? query.OrderBy(b => b.Year) : query.OrderByDescending(b => b.Year),
                BookSortBy.CopiesCount => asc ? query.OrderBy(b => b.CopiesCount) : query.OrderByDescending(b => b.CopiesCount),
                _ => asc ? query.OrderBy(b => b.Id) : query.OrderByDescending(b => b.Id)
            };

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BookDto
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    ISBN = b.ISBN,
                    Category = b.Category,
                    Year = b.Year,
                    CopiesCount = b.CopiesCount,
                    ActiveBorrowCount = b.BorrowRecords.Count(br => br.ReturnedDate == null),
                    AvailableCopies = b.CopiesCount - b.BorrowRecords.Count(br => br.ReturnedDate == null),
                    BorrowCount = qp.IncludeBorrowCount
                        ? b.BorrowRecords.Count(br => br.ReturnedDate == null)
                        : (int?)null
                })
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<List<BookDto>> GetForExportAsync(BookQueryParams qp, int maxRows = 10000, CancellationToken ct = default)
        {
            var (items, _) = await GetPagedAsync(new BookQueryParams
            {
                Q = qp.Q,
                Title = qp.Title,
                Author = qp.Author,
                Category = qp.Category,
                Isbn = qp.Isbn,
                IsbnStartsWith = qp.IsbnStartsWith,
                YearFrom = qp.YearFrom,
                YearTo = qp.YearTo,
                MinCopies = qp.MinCopies,
                MaxCopies = qp.MaxCopies,
                SortBy = qp.SortBy,
                SortDir = qp.SortDir,
                Page = 1,
                PageSize = maxRows,
                IncludeBorrowCount = qp.IncludeBorrowCount
            }, ct);

            return items;
        }

        public async Task<BookDto?> GetByIdAsync(int id)
        {
            return await _context.Books.AsNoTracking()
                .Where(b => b.Id == id)
                .Select(b => new BookDto
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    ISBN = b.ISBN,
                    Category = b.Category,
                    Year = b.Year,
                    CopiesCount = b.CopiesCount,
                    ActiveBorrowCount = b.BorrowRecords.Count(br => br.ReturnedDate == null),
                    AvailableCopies = b.CopiesCount - b.BorrowRecords.Count(br => br.ReturnedDate == null),
                    BorrowCount = b.BorrowRecords.Count(br => br.ReturnedDate == null)
                })
                .FirstOrDefaultAsync();
        }

        public async Task<Book> AddAsync(BookCreateDto dto)
        {
            var title = dto.Title.Trim();
            var author = dto.Author.Trim();
            var category = dto.Category.Trim();
            var isbnRaw = dto.ISBN.Trim();

            var isbnNorm = NormalizeIsbnInput(isbnRaw);

            // تحقق صحة ISBN بعد التطبيع
            if (!(isbnNorm.Length == 10 || isbnNorm.Length == 13) ||
                !isbnNorm.Take(isbnNorm.Length - 1).All(char.IsDigit) ||
                !(char.IsDigit(isbnNorm.Last()) || (isbnNorm.Length == 10 && (isbnNorm.Last() == 'x' || isbnNorm.Last() == 'X'))))
            {
                throw new InvalidOperationException("يجب أن يكون طول ISBN 10 أو 13 رقمًا (والحرف X مسموح فقط كآخر محرف في ISBN-10) بعد إزالة الفراغات والشرطات.");
            }

            // منع تكرار ISBN
            var existsIsbn = await _context.Books.AsNoTracking()
                .AnyAsync(b =>
                    ((b.ISBN ?? "").Replace("-", "").Replace(" ", "").ToLower()) == isbnNorm);

            if (existsIsbn)
                throw new InvalidOperationException($"الرقم الدولي '{isbnRaw}' موجود مسبقًا.");

            // منع تكرار (العنوان+المؤلف+السنة)
            var existsTitleAuthor = await _context.Books.AsNoTracking()
                .AnyAsync(b => b.Title == title && b.Author == author && b.Year == dto.Year);
            if (existsTitleAuthor)
                throw new InvalidOperationException($"كتاب بعنوان '{title}' للمؤلف '{author}' (سنة {dto.Year}) موجود مسبقًا.");

            if (dto.CopiesCount < 0)
                throw new InvalidOperationException("لا يمكن أن يكون عدد النسخ سالبًا.");

            var book = new Book
            {
                Title = title,
                Author = author,
                Category = category,
                Year = dto.Year,
                CopiesCount = dto.CopiesCount,
                ISBN = isbnRaw
            };

            _context.Books.Add(book);
            await _context.SaveChangesAsync();
            return book;
        }

        public async Task<bool> UpdateAsync(int id, BookUpdateDto dto)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null) return false;

            var title = dto.Title.Trim();
            var author = dto.Author.Trim();
            var category = dto.Category.Trim();
            var isbnRaw = dto.ISBN.Trim();
            var isbnNorm = NormalizeIsbnInput(isbnRaw);

            if (!(isbnNorm.Length == 10 || isbnNorm.Length == 13) ||
                !isbnNorm.Take(isbnNorm.Length - 1).All(char.IsDigit) ||
                !(char.IsDigit(isbnNorm.Last()) || (isbnNorm.Length == 10 && (isbnNorm.Last() == 'x' || isbnNorm.Last() == 'X'))))
            {
                throw new InvalidOperationException("يجب أن يكون طول ISBN 10 أو 13 رقمًا (والحرف X مسموح فقط كآخر محرف في ISBN-10) بعد إزالة الفراغات والشرطات.");
            }

            var isbnTaken = await _context.Books.AsNoTracking()
                .AnyAsync(b =>
                    b.Id != id &&
                    ((b.ISBN ?? "").Replace("-", "").Replace(" ", "").ToLower()) == isbnNorm);

            if (isbnTaken)
                throw new InvalidOperationException($"لا يمكن التحديث: الرقم الدولي '{isbnRaw}' مستخدم في كتاب آخر.");

            var duplicate = await _context.Books.AsNoTracking()
                .AnyAsync(b => b.Id != id && b.Title == title && b.Author == author && b.Year == dto.Year);
            if (duplicate)
                throw new InvalidOperationException($"لا يمكن التحديث: كتاب بعنوان '{title}' للمؤلف '{author}' (سنة {dto.Year}) موجود مسبقًا.");

            if (dto.CopiesCount < 0)
                throw new InvalidOperationException("لا يمكن أن يكون عدد النسخ سالبًا.");

            book.Title = title;
            book.Author = author;
            book.Category = category;
            book.Year = dto.Year;
            book.CopiesCount = dto.CopiesCount;
            book.ISBN = isbnRaw;

            await _context.SaveChangesAsync();
            return true;
        }

        private static string NormalizeIsbnInput(string? isbn)
        {
            if (string.IsNullOrWhiteSpace(isbn)) return string.Empty;
            var norm = new string(isbn.Where(ch => ch != ' ' && ch != '-').ToArray()).ToLowerInvariant();
            return norm;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var book = await _context.Books
                .Include(b => b.BorrowRecords)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (book == null) return false;

            if (book.BorrowRecords?.Count > 0)
                throw new InvalidOperationException("لا يمكن حذف كتاب لديه سجلات استعارة.");

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
