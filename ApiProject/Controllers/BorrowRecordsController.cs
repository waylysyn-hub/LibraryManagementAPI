using Data.Services;
using Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExportRow = Data.Services.BorrowRecordService.BorrowRecordExportRow;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BorrowRecordsController : ControllerBase
    {
        private readonly BorrowRecordService _service;
        private readonly ILogger<BorrowRecordsController> _logger;

        public BorrowRecordsController(BorrowRecordService service, ILogger<BorrowRecordsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [Authorize(Policy = "borrow.read")]
        [HttpGet]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? memberId,
            [FromQuery] int? bookId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            try
            {
                page = page < 1 ? 1 : page;
                pageSize = pageSize <= 0 ? 50 : (pageSize > 200 ? 200 : pageSize);

                var (items, total) = await _service.GetPagedAsync(memberId, bookId, page, pageSize, ct);
                if (items.Count == 0)
                    return NotFound(new { success = false, message = "لا توجد سجلات إعارة" });

                var totalPages = (int)Math.Ceiling(total / (double)pageSize);

                return Ok(new
                {
                    success = true,
                    data = items,
                    meta = new
                    {
                        page,
                        pageSize,
                        total,
                        totalPages
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching borrow records");
                return StatusCode(500, new { success = false, message = "حدث خطأ غير متوقع أثناء جلب سجلات الإعارة", details = ex.Message });
            }
        }

        [Authorize(Policy = "borrow.read")]
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
        {
            try
            {
                var borrowRecord = await _service.GetByIdAsync(id, ct);
                if (borrowRecord == null)
                    return NotFound(new { success = false, message = $"سجل الإعارة {id} غير موجود" });

                return Ok(new { success = true, data = borrowRecord });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching borrow record {RecordId}", id);
                return StatusCode(500, new { success = false, message = $"حدث خطأ غير متوقع أثناء جلب سجل الإعارة {id}", details = ex.Message });
            }
        }

        [Authorize(Policy = "borrow.create")]
        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] BorrowRecordCreateDto dto, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "فشل التحقق من صحة البيانات",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            try
            {
                var borrowRecord = await _service.AddAsync(dto, ct);
                return CreatedAtAction(nameof(GetById), new { id = borrowRecord.Id }, new
                {
                    success = true,
                    message = "تم إنشاء سجل الإعارة بنجاح",
                    id = borrowRecord.Id
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Create validation error");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Create conflict");
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating borrow record");
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع أثناء إنشاء سجل الإعارة", details = ex.Message });
            }
        }

        [Authorize(Policy = "borrow.update")]
        [HttpPut("{id:int}")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(int id, [FromBody] BorrowRecordUpdateDto dto, CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "فشل التحقق من صحة البيانات",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            try
            {
                var updated = await _service.UpdateAsync(id, dto, ct);
                if (!updated)
                    return NotFound(new { success = false, message = $"سجل الإعارة {id} غير موجود" });

                return Ok(new { success = true, message = $"تم تحديث سجل الإعارة {id} بنجاح" });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Update validation error for {RecordId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Update conflict for {RecordId}", id);
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating borrow record {RecordId}", id);
                return StatusCode(500, new { success = false, message = $"حدث خطأ غير متوقع أثناء تحديث سجل الإعارة {id}", details = ex.Message });
            }
        }

        [Authorize(Policy = "borrow.delete")]
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            try
            {
                var deleted = await _service.DeleteAsync(id, ct);
                if (!deleted)
                    return NotFound(new { success = false, message = $"سجل الإعارة {id} غير موجود" });

                return Ok(new { success = true, message = $"تم حذف سجل الإعارة {id} بنجاح" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting borrow record {RecordId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = $"حدث خطأ غير متوقع أثناء حذف سجل الإعارة {id}",
                    details = ex.Message
                });
            }
        }

        [Authorize(Policy = "borrow.update")]
        [HttpPost("{id:int}/return")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Return(int id, CancellationToken ct = default)
        {
            try
            {
                var ok = await _service.ReturnAsync(id, ct);
                if (!ok)
                    return NotFound(new { success = false, message = $"سجل الإعارة {id} غير موجود" });

                return Ok(new { success = true, message = "تم إرجاع الكتاب بنجاح." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Return conflict for {RecordId}", id);
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error returning borrow record {RecordId}", id);
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع أثناء إرجاع الكتاب", details = ex.Message });
            }
        }

        [Authorize(Policy = "borrow.read")]
        [HttpGet("export")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Export(
            [FromQuery] int? memberId,
            [FromQuery] int? bookId,
            CancellationToken ct = default)
        {
            try
            {
                var rows = await _service.GetForExportAsync(memberId, bookId, ct);
                if (rows.Count == 0) return NoContent();

                List<(string Header, Func<ExportRow, object?>)> headers = new()
                {
                    ("المعرّف",        x => x.Id),
                    ("معرّف العضو",    x => x.MemberId),
                    ("اسم العضو",      x => x.MemberName ?? ""),
                    ("معرّف الكتاب",   x => x.BookId),
                    ("عنوان الكتاب",   x => x.BookTitle ?? ""),
                    ("تاريخ الإعارة",  x => x.BorrowedDate),
                    ("تاريخ الاستحقاق",x => x.DueDate),
                    ("تاريخ الإرجاع",  x => x.ReturnedDate),
                    ("الحالة",         x => x.Status),
                    ("أيام التأخير",   x => x.OverdueDays)
                };

                var dateFormat = "yyyy-MM-dd HH:mm";
                var stream = ExcelExportService
                    .ExportToExcel<ExportRow>(rows, headers, "BorrowRecords", dateFormat);

                var fileName = $"borrow_records_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                const string ctExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return File(stream.ToArray(), ctExcel, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting borrow records");
                return StatusCode(500, new { success = false, message = "خطأ أثناء إنشاء ملف الإكسل", details = ex.Message });
            }
        }
    }
}
