using System.Globalization;
using System.Linq;
using Data;
using Data.Services;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly BankDbContext _db;
        private readonly IWebHostEnvironment _env;
        private static bool _pdfFontsRegistered = false;
        private static readonly object _lock = new();

        public ReportsController(BankDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ========================= Helpers =========================
        private IQueryable<BorrowRecord> BuildBorrowQuery(
            int? memberId, int? bookId, DateTime? fromDate, DateTime? toDate)
        {
            var q = _db.BorrowRecords
                .AsNoTracking()
                .Include(br => br.Book)
                .Include(br => br.Member)
                .AsQueryable();

            if (memberId.HasValue) q = q.Where(br => br.MemberId == memberId.Value);
            if (bookId.HasValue) q = q.Where(br => br.BookId == bookId.Value);

            if (fromDate.HasValue)
            {
                var f = fromDate.Value.Date;
                q = q.Where(br => br.BorrowedDate >= f);
            }

            if (toDate.HasValue)
            {
                // نهاية اليوم المحدد
                var t = toDate.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(br => br.BorrowedDate <= t);
            }

            return q.AsSplitQuery();
        }

        private async Task<List<BorrowRecordExportRow>> ProjectBorrowRows(
            IQueryable<BorrowRecord> query, CancellationToken ct)
        {
            var todayUtc = DateTime.UtcNow.Date;

            return await query
                .OrderByDescending(x => x.BorrowedDate)
                .Select(br => new BorrowRecordExportRow
                {
                    Id = br.Id,
                    MemberId = br.MemberId,
                    MemberName = br.Member.Name,
                    BookId = br.BookId,
                    BookTitle = br.Book.Title,
                    BorrowedDate = br.BorrowedDate,
                    DueDate = br.DueDate,
                    ReturnedDate = br.ReturnedDate, // nullable
                    Status = br.ReturnedDate.HasValue ? "تم الإرجاع" : "مستعار",
                    OverdueDays = br.ReturnedDate.HasValue
                        ? Math.Max(0, (int)EF.Functions.DateDiffDay(br.DueDate, br.ReturnedDate.Value))
                        : Math.Max(0, (int)EF.Functions.DateDiffDay(br.DueDate, todayUtc))
                })
                .ToListAsync(ct);
        }

        private void EnsurePdfFontsRegistered()
        {
            if (_pdfFontsRegistered) return;
            lock (_lock)
            {
                if (_pdfFontsRegistered) return;

                var root = _env.WebRootPath ?? "";
                var fonts = Path.Combine(root, "fonts");
                var regPath = Path.Combine(fonts, "NotoNaskhArabic-Regular.ttf");
                var bldPath = Path.Combine(fonts, "NotoNaskhArabic-Bold.ttf");

                if (!System.IO.File.Exists(regPath) || !System.IO.File.Exists(bldPath))
                    throw new FileNotFoundException("ملفات الخطوط غير موجودة ضمن wwwroot/fonts.");

                using var reg = System.IO.File.OpenRead(regPath);
                using var bld = System.IO.File.OpenRead(bldPath);
                QuestPDF.Drawing.FontManager.RegisterFont(reg);
                QuestPDF.Drawing.FontManager.RegisterFont(bld);

                TextStyle.Default.FontFamily("Noto Naskh Arabic");
                _pdfFontsRegistered = true;
            }
        }

        private static TextStyle ArabicBaseStyle => TextStyle.Default
            .FontFamily("Noto Naskh Arabic")
            .FontSize(11)
            .DirectionFromRightToLeft();

        // ========================= Borrow Records (browse & export) =========================

        /// <summary>تصفية واستعراض سجلات الاستعارة</summary>
        [HttpGet("borrow-records")]
        public async Task<IActionResult> GetFilteredBorrowRecords(
            int? memberId, int? bookId, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
        {
            try
            {
                if (memberId.HasValue)
                {
                    var memberExists = await _db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId.Value, ct);
                    if (!memberExists)
                        return Ok(new { success = true, data = Array.Empty<BorrowRecordExportRow>(), message = "العضو المحدد غير موجود." });
                }

                if (bookId.HasValue)
                {
                    var bookExists = await _db.Books.AsNoTracking().AnyAsync(b => b.Id == bookId.Value, ct);
                    if (!bookExists)
                        return Ok(new { success = true, data = Array.Empty<BorrowRecordExportRow>(), message = "الكتاب المحدد غير موجود." });
                }

                var query = BuildBorrowQuery(memberId, bookId, fromDate, toDate);
                var borrowRecords = await ProjectBorrowRows(query, ct);

                if (!borrowRecords.Any())
                    return Ok(new { success = true, data = Array.Empty<BorrowRecordExportRow>(), message = "لا توجد سجلات استعارة مطابقة." });

                return Ok(new { success = true, data = borrowRecords });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ غير متوقع أثناء استعراض السجلات.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }

        [HttpGet("borrow-records/export-pdf")]
        public async Task<IActionResult> ExportBorrowRecordsPdf(
            int? memberId, int? bookId, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
        {
            try
            {
                if (memberId.HasValue && !await _db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId.Value, ct))
                    return Ok(new { success = true, data = Array.Empty<BorrowRecordExportRow>(), message = "العضو المحدد غير موجود." });

                if (bookId.HasValue && !await _db.Books.AsNoTracking().AnyAsync(b => b.Id == bookId.Value, ct))
                    return Ok(new { success = true, data = Array.Empty<BorrowRecordExportRow>(), message = "الكتاب المحدد غير موجود." });

                var rows = await ProjectBorrowRows(BuildBorrowQuery(memberId, bookId, fromDate, toDate), ct);
                if (!rows.Any())
                    return Ok(new { success = true, data = Array.Empty<BorrowRecordExportRow>(), message = "لا توجد سجلات مطابقة." });

                EnsurePdfFontsRegistered();

                // ألوان بسيطة متوافقة
                var Primary = "#1d4ed8";
                var PrimarySoft = "#eef2ff";
                var Success = "#16a34a";
                var Warning = "#f59e0b";
                var Danger = "#ef4444";
                var TextDim = "#64748b";

                var today = DateTime.UtcNow.Date;
                int total = rows.Count;
                int returned = rows.Count(r => r.ReturnedDate.HasValue);
                int active = rows.Count(r => !r.ReturnedDate.HasValue);
                int overdue = rows.Count(r => !r.ReturnedDate.HasValue && r.DueDate.Date < today);

                string RangeText()
                {
                    string f = fromDate.HasValue ? fromDate.Value.ToString("yyyy/MM/dd") : "—";
                    string t = toDate.HasValue ? toDate.Value.ToString("yyyy/MM/dd") : "—";
                    return $"{f}  →  {t}";
                }

                string FiltersLine()
                {
                    var parts = new List<string>();
                    if (memberId.HasValue) parts.Add($"العضو: #{memberId.Value}");
                    if (bookId.HasValue) parts.Add($"الكتاب: #{bookId.Value}");
                    parts.Add($"النطاق الزمني: {RangeText()}");
                    return string.Join("   |   ", parts);
                }

                byte[] pdf = Document.Create(doc =>
                {
                    doc.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30);
                        page.DefaultTextStyle(ArabicBaseStyle);

                        // ===== Header =====
                        page.Header().Column(col =>
                        {
                            col.Spacing(6);

                            col.Item().Row(row =>
                            {
                                row.RelativeItem().AlignRight().Text(t =>
                                {
                                    t.Span("تقرير سجلات الاستعارة").FontSize(18).Bold().FontColor(Primary);
                                    t.Line("");
                                    t.Span($"تاريخ التوليد: {DateTime.Now:yyyy/MM/dd HH:mm}").FontSize(9).FontColor(TextDim);
                                });

                                row.ConstantItem(120).AlignLeft().PaddingTop(2).Column(c2 =>
                                {
                                    c2.Item().AlignLeft().Text(t =>
                                    {
                                        t.Span("Library").Bold().FontSize(12).FontColor(Primary);
                                        t.Span(" • Reports").FontSize(10).FontColor(TextDim);
                                    });
                                });
                            });

                            col.Item().PaddingTop(6).BorderBottom(1).BorderColor(Primary);
                        });

                        // ===== Content =====
                        page.Content().Column(col =>
                        {
                            col.Spacing(12);

                            // بطاقات ملخص (بدون تدوير حواف لتوافق API قديمة)
                            col.Item().Row(row =>
                            {
                                void StatCard(string title, string value, string? colorHex = null)
                                {
                                    row.RelativeItem().Element(card =>
                                    {
                                        card.Padding(10)
                                            .Background(PrimarySoft)
                                            .Border(1).BorderColor("#e5e7eb")
                                            .Column(c =>
                                            {
                                                c.Spacing(3);
                                                c.Item().Text(title).FontSize(9).FontColor(TextDim);
                                                c.Item().Text(value).FontSize(16).Bold().FontColor(colorHex ?? Primary);
                                            });
                                    });
                                }

                                StatCard("إجمالي السجلات", total.ToString(), Primary);
                                StatCard("نشِط (غير مُعاد)", active.ToString(), Warning);
                                StatCard("متأخر", overdue.ToString(), Danger);
                                StatCard("تم الإرجاع", returned.ToString(), Success);
                            });

                            // سطر الفلاتر
                            col.Item().PaddingTop(2).Text(FiltersLine()).FontSize(10).FontColor(TextDim);

                            // جدول
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(1);   // #
                                    c.RelativeColumn(2);   // العضو
                                    c.RelativeColumn(3);   // الكتاب
                                    c.RelativeColumn(1.4f);// الاستعارة
                                    c.RelativeColumn(1.4f);// الاستحقاق
                                    c.RelativeColumn(1.4f);// الإرجاع
                                    c.RelativeColumn(1.4f);// الحالة
                                    c.RelativeColumn(1.2f);// التأخير
                                });

                                table.Header(h =>
                                {
                                    void H(string txt) => h.Cell()
                                        .Background(PrimarySoft)
                                        .BorderBottom(1).BorderColor("#e5e7eb")
                                        .PaddingVertical(6).PaddingHorizontal(5)
                                        .AlignCenter().Text(txt).Bold();

                                    H("#");
                                    H("العضو");
                                    H("الكتاب");
                                    H("الاستعارة");
                                    H("الاستحقاق");
                                    H("الإرجاع");
                                    H("الحالة");
                                    H("أيام التأخير");
                                });

                                for (int i = 0; i < rows.Count; i++)
                                {
                                    var r = rows[i];
                                    bool isOdd = i % 2 == 1;
                                    bool isOverdue = !r.ReturnedDate.HasValue && r.DueDate.Date < today;

                                    string BadgeColor()
                                    {
                                        if (r.ReturnedDate.HasValue) return Success;
                                        if (isOverdue) return Danger;
                                        return Warning;
                                    }

                                    string overdueText = r.OverdueDays > 0 ? r.OverdueDays.ToString() : "—";

                                    void Cell(Action<IContainer> content, bool center = false)
                                    {
                                        var baseCell = table.Cell()
                                            .Background(isOdd ? "#fafafa" : "#ffffff")
                                            .BorderBottom(1).BorderColor("#f1f5f9")
                                            .PaddingVertical(6).PaddingHorizontal(5);

                                        baseCell = center ? baseCell.AlignCenter() : baseCell.AlignRight();
                                        baseCell.Element(content);
                                    }

                                    // #
                                    Cell(c => c.Text(r.Id.ToString()).FontColor(TextDim), center: true);

                                    // العضو
                                    Cell(c => c.Text($"{r.MemberName} (#{r.MemberId})"));

                                    // الكتاب
                                    Cell(c => c.Text($"{r.BookTitle} (#{r.BookId})"));

                                    // الاستعارة
                                    Cell(c => c.AlignCenter().Text(
                                        r.BorrowedDate == default
                                            ? "—"
                                            : r.BorrowedDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
                                    ), center: true);

                                    // الاستحقاق
                                    Cell(c =>
                                    {
                                        var txt = r.DueDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                                        var cont = c.AlignCenter();
                                        if (isOverdue) cont.Text(txt).Bold().FontColor(Danger);
                                        else cont.Text(txt);
                                    }, center: true);

                                    // الإرجاع
                                    Cell(c => c.AlignCenter().Text(
                                        r.ReturnedDate.HasValue
                                            ? r.ReturnedDate.Value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
                                            : "—"
                                    ), center: true);

                                    // الحالة (بادج بسيطة بدون تدوير)
                                    Cell(c =>
                                    {
                                        c.AlignCenter().PaddingTop(2).Row(rr =>
                                        {
                                            rr.RelativeItem().AlignCenter().Element(b =>
                                                b.PaddingVertical(3).PaddingHorizontal(8)
                                                 .Background(BadgeColor())
                                                 .Text(r.Status).FontSize(9).Bold().FontColor("#ffffff")
                                            );
                                        });
                                    }, center: true);

                                    // أيام التأخير
                                    Cell(c =>
                                    {
                                        var cont = c.AlignCenter();
                                        if (isOverdue) cont.Text(overdueText).Bold().FontColor(Danger);
                                        else cont.Text(overdueText);
                                    }, center: true);
                                }
                            });

                            if (overdue > 0)
                                col.Item().PaddingTop(6).Text($"⚠ يوجد {overdue} سجل/سجلات متأخرة.")
                                    .FontSize(10).FontColor(Danger);
                        });

                        // ===== Footer =====
                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("صفحة ").FontSize(10).FontColor(TextDim);
                            x.CurrentPageNumber().FontSize(10).FontColor(TextDim);
                            x.Span(" / ").FontSize(10).FontColor(TextDim);
                            x.TotalPages().FontSize(10).FontColor(TextDim);
                        });

                    });
                }).GeneratePdf();

                var fileName = $"borrow-records-{DateTime.UtcNow:yyyyMMdd}.pdf";
                return File(pdf, "application/pdf", fileName);
            }
            catch (FileNotFoundException fnf)
            {
                return StatusCode(500, new { success = false, message = "خطوط PDF العربية غير موجودة ضمن wwwroot/fonts.", details = fnf.Message, trace = HttpContext.TraceIdentifier });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء إنشاء ملف PDF.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }

        /// <summary>تصدير سجلات الاستعارة إلى Excel (باستخدام ExcelExportService)</summary>
        [HttpGet("borrow-records/export-xlsx")]
        public async Task<IActionResult> ExportBorrowRecordsXlsx(
            int? memberId, int? bookId, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
        {
            try
            {
                var rows = await ProjectBorrowRows(
                    BuildBorrowQuery(memberId, bookId, fromDate, toDate), ct);

                if (!rows.Any())
                {
                    // نعيد ملفًا فارغًا مع رأس الجدول فقط (اختياريًا) أو رسالة
                    return Ok(new { success = true, data = Array.Empty<BorrowRecordExportRow>(), message = "لا توجد سجلات مطابقة." });
                }

                var headers = new List<(string Header, Func<BorrowRecordExportRow, object?> ValueSelector)>
                {
                    ("#", x => x.Id),
                    ("العضو", x => $"{x.MemberName} (#{x.MemberId})"),
                    ("الكتاب", x => $"{x.BookTitle} (#{x.BookId})"),
                    ("الاستعارة", x => x.BorrowedDate == default
                        ? "—"
                        : x.BorrowedDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)),
                    ("الاستحقاق", x => x.DueDate == default
                        ? "—"
                        : x.DueDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)),
                    ("الإرجاع", x => x.ReturnedDate == null
                        ? "—"
                        : x.ReturnedDate.Value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)),
                    ("الحالة", x => x.Status),
                    ("أيام التأخير", x => x.OverdueDays)
                };

                var stream = ExcelExportService.ExportToExcel(rows, headers, "BorrowRecords", "BorrowRecordsTable");
                var fileName = $"borrow-records-{DateTime.UtcNow:yyyyMMdd}.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء إنشاء ملف Excel.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }

        // ========================= إحصاءات (Dashboard) =========================
        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats(CancellationToken ct)
        {
            try
            {
                var todayUtc = DateTime.UtcNow.Date;

                var totalBooks = await _db.Books.AsNoTracking().CountAsync(ct);
                var totalMembers = await _db.Members.AsNoTracking().CountAsync(ct);
                var borrowedBooks = await _db.BorrowRecords.AsNoTracking().CountAsync(br => br.ReturnedDate == null, ct);
                var overdueBooks = await _db.BorrowRecords.AsNoTracking().CountAsync(br => br.ReturnedDate == null && br.DueDate < todayUtc, ct);

                return Ok(new { totalBooks, totalMembers, borrowedBooks, overdueBooks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ غير متوقع أثناء جلب الإحصائيات.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }

        // ========================= أكثر الكتب استعارة =========================
        [HttpGet("most-borrowed-books")]
        public async Task<IActionResult> GetMostBorrowedBooks([FromQuery] int limit = 10, CancellationToken ct = default)
        {
            try
            {
                if (limit <= 0)
                    return BadRequest(new { success = false, message = "عدد الكتب المطلوب يجب أن يكون أكبر من صفر." });

                var data = await _db.BorrowRecords
                    .AsNoTracking()
                    .Include(br => br.Book)
                    .GroupBy(br => new { br.BookId, br.Book.Title })
                    .Select(g => new
                    {
                        bookId = g.Key.BookId,
                        title = g.Key.Title,
                        borrowCount = g.Count()
                    })
                    .OrderByDescending(x => x.borrowCount)
                    .Take(limit)
                    .ToListAsync(ct);

                return Ok(data); // دائمًا 200
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء جلب أكثر الكتب استعارة.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }

        // ========================= الكتب المتأخرة =========================
        [HttpGet("overdue-books")]
        public async Task<IActionResult> GetOverdueBooks(CancellationToken ct)
        {
            try
            {
                var todayUtc = DateTime.UtcNow.Date;

                var overdue = await _db.BorrowRecords
                    .AsNoTracking()
                    .Include(br => br.Book)
                    .Include(br => br.Member)
                    .Where(br => br.ReturnedDate == null && br.DueDate < todayUtc)
                    .Select(br => new
                    {
                        id = br.Id,
                        bookTitle = br.Book.Title,
                        memberName = br.Member.Name,
                        memberEmail = br.Member.Email,
                        memberPhone = br.Member.Phone,
                        dueDate = br.DueDate,
                        daysLate = EF.Functions.DateDiffDay(br.DueDate.Date, todayUtc)
                    })
                    .OrderByDescending(br => br.daysLate)
                    .ToListAsync(ct);

                return Ok(overdue); // دائمًا 200
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء جلب الكتب المتأخرة.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }

        // ========================= الأعضاء الأكثر استعارة =========================
        [HttpGet("active-members")]
        public async Task<IActionResult> GetMostActiveMembers([FromQuery] int limit = 10, CancellationToken ct = default)
        {
            try
            {
                if (limit <= 0)
                    return BadRequest(new { success = false, message = "عدد الأعضاء المطلوب يجب أن يكون أكبر من صفر." });

                var data = await _db.BorrowRecords
                    .AsNoTracking()
                    .Include(br => br.Member)
                    .GroupBy(br => new { br.MemberId, br.Member.Name })
                    .Select(g => new
                    {
                        memberId = g.Key.MemberId,
                        name = g.Key.Name,
                        totalBorrowings = g.Count()
                    })
                    .OrderByDescending(x => x.totalBorrowings)
                    .Take(limit)
                    .ToListAsync(ct);

                return Ok(data); // دائمًا 200
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء جلب الأعضاء الأكثر استعارة.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }

        // ========================= أفكار جديدة: Trend / Categories / Cohorts =========================

        /// <summary>
        /// اتجاهات الاستعارة/الإرجاع ضمن فترة، مجمّعة حسب day|week|month.
        /// أمثلة:
        ///   GET borrowing-trend?from=2025-07-01&to=2025-10-21&bucket=day
        /// </summary>
        [HttpGet("borrowing-trend")]
        public async Task<IActionResult> BorrowingTrend(DateTime from, DateTime to, string bucket = "day", CancellationToken ct = default)
        {
            try
            {
                if (from == default || to == default || to < from)
                    return BadRequest(new { success = false, message = "نطاق التاريخ غير صالح." });

                var f = from.Date;
                var t = to.Date.AddDays(1).AddTicks(-1); // نهاية اليوم الأخير

                var mode = (bucket ?? "day").Trim().ToLowerInvariant();

                // ====== DAY ======
                if (mode == "day")
                {
                    var borrows = await _db.BorrowRecords.AsNoTracking()
                        .Where(x => x.BorrowedDate >= f && x.BorrowedDate <= t)
                        .GroupBy(x => x.BorrowedDate.Date)
                        .Select(g => new { key = g.Key, count = g.Count() })
                        .ToListAsync(ct);

                    var returns = await _db.BorrowRecords.AsNoTracking()
                        .Where(x => x.ReturnedDate != null && x.ReturnedDate >= f && x.ReturnedDate <= t)
                        .GroupBy(x => x.ReturnedDate!.Value.Date)
                        .Select(g => new { key = g.Key, count = g.Count() })
                        .ToListAsync(ct);

                    var allKeys = borrows.Select(b => b.key).Union(returns.Select(r => r.key)).Distinct().OrderBy(d => d);
                    var db = borrows.ToDictionary(x => x.key, x => x.count);
                    var dr = returns.ToDictionary(x => x.key, x => x.count);

                    var points = allKeys.Select(k => new
                    {
                        date = k.ToString("yyyy-MM-dd"),
                        borrow = db.TryGetValue(k, out var b) ? b : 0,
                        // اسم الحقل "return" في JSON
                        @return = dr.TryGetValue(k, out var r) ? r : 0
                    });

                    return Ok(points);
                }

                // ====== MONTH ======
                if (mode == "month")
                {
                    var borrows = await _db.BorrowRecords.AsNoTracking()
                        .Where(x => x.BorrowedDate >= f && x.BorrowedDate <= t)
                        .GroupBy(x => new { x.BorrowedDate.Year, x.BorrowedDate.Month })
                        .Select(g => new { g.Key.Year, g.Key.Month, count = g.Count() })
                        .ToListAsync(ct);

                    var returns = await _db.BorrowRecords.AsNoTracking()
                        .Where(x => x.ReturnedDate != null && x.ReturnedDate >= f && x.ReturnedDate <= t)
                        .GroupBy(x => new { Year = x.ReturnedDate!.Value.Year, Month = x.ReturnedDate!.Value.Month })
                        .Select(g => new { g.Key.Year, g.Key.Month, count = g.Count() })
                        .ToListAsync(ct);

                    // نبني مفتاح الشهر كـ DateTime أول يوم بالشهر
                    var bKeys = borrows.Select(x => new DateTime(x.Year, x.Month, 1));
                    var rKeys = returns.Select(x => new DateTime(x.Year, x.Month, 1));
                    var allKeys = bKeys.Union(rKeys).Distinct().OrderBy(d => d);

                    var db = borrows.ToDictionary(x => new DateTime(x.Year, x.Month, 1), x => x.count);
                    var dr = returns.ToDictionary(x => new DateTime(x.Year, x.Month, 1), x => x.count);

                    var points = allKeys.Select(k => new
                    {
                        date = k.ToString("yyyy-MM-dd"),
                        borrow = db.TryGetValue(k, out var b) ? b : 0,
                        @return = dr.TryGetValue(k, out var r) ? r : 0
                    });

                    return Ok(points);
                }

                // ====== WEEK (تجميع أسبوعي يبدأ الاثنين) — نجيب اليومي من السيرفر ونجمعه محلياً ======
                {
                    var dailyBorrows = await _db.BorrowRecords.AsNoTracking()
                        .Where(x => x.BorrowedDate >= f && x.BorrowedDate <= t)
                        .GroupBy(x => x.BorrowedDate.Date)
                        .Select(g => new { day = g.Key, count = g.Count() })
                        .ToListAsync(ct);

                    var dailyReturns = await _db.BorrowRecords.AsNoTracking()
                        .Where(x => x.ReturnedDate != null && x.ReturnedDate >= f && x.ReturnedDate <= t)
                        .GroupBy(x => x.ReturnedDate!.Value.Date)
                        .Select(g => new { day = g.Key, count = g.Count() })
                        .ToListAsync(ct);

                    static DateTime WeekStartMonday(DateTime d)
                    {
                        // Monday=0 ... Sunday=6
                        int diff = ((int)d.DayOfWeek + 6) % 7;
                        return d.Date.AddDays(-diff);
                    }

                    var bWeek = dailyBorrows
                        .GroupBy(x => WeekStartMonday(x.day))
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.count));

                    var rWeek = dailyReturns
                        .GroupBy(x => WeekStartMonday(x.day))
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.count));

                    var allKeys = bWeek.Keys.Union(rWeek.Keys).Distinct().OrderBy(d => d);

                    var points = allKeys.Select(k => new
                    {
                        date = k.ToString("yyyy-MM-dd"),
                        borrow = bWeek.TryGetValue(k, out var b) ? b : 0,
                        @return = rWeek.TryGetValue(k, out var r) ? r : 0
                    });

                    return Ok(points);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء توليد اتجاهات الاستعارة.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }

        /// <summary>
        /// أكثر التصنيفات استعارةً مع الحصة النسبية.
        ///   GET categories-top?limit=10
        /// </summary>
        [HttpGet("categories-top")]
        public async Task<IActionResult> CategoriesTop([FromQuery] int limit = 10, CancellationToken ct = default)
        {
            try
            {
                if (limit <= 0)
                    return BadRequest(new { success = false, message = "الحدّ يجب أن يكون أكبر من صفر." });

                var total = await _db.BorrowRecords.AsNoTracking().CountAsync(ct);

                var rows = await _db.BorrowRecords
                    .AsNoTracking()
                    .Include(x => x.Book)
                    .Where(x => x.Book.Category != null)
                    .GroupBy(x => x.Book.Category!)
                    .Select(g => new
                    {
                        category = g.Key,
                        count = g.Count(),
                        share = total == 0 ? 0 : (double)g.Count() / total
                    })
                    .OrderByDescending(x => x.count)
                    .Take(limit)
                    .ToListAsync(ct);

                return Ok(rows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء جلب إحصاءات التصنيفات.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }

     
        [HttpGet("member-cohorts")]
        public async Task<IActionResult> MemberCohorts([FromQuery] int days = 90, CancellationToken ct = default)
        {
            try
            {
                if (days <= 0 || days > 3650)
                    return BadRequest(new { success = false, message = "القيمة days غير صالحة." });

                var from = DateTime.UtcNow.Date.AddDays(-days);

                var counts = await _db.BorrowRecords.AsNoTracking()
                    .Where(x => x.BorrowedDate >= from)
                    .GroupBy(x => x.MemberId)
                    .Select(g => new { memberId = g.Key, c = g.Count() })
                    .ToListAsync(ct);

                var veryActive = counts.Count(x => x.c >= 12);
                var active = counts.Count(x => x.c >= 6 && x.c < 12);
                var light = counts.Count(x => x.c >= 1 && x.c < 6);

                var totalMembers = await _db.Members.AsNoTracking().CountAsync(ct);
                var inactive = Math.Max(0, totalMembers - (veryActive + active + light));

                return Ok(new { veryActive, active, light, inactive, totalMembers, days });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء حساب شرائح الأعضاء.", details = ex.Message, trace = HttpContext.TraceIdentifier });
            }
        }
    }
}
