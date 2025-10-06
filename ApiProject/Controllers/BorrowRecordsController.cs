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

        // ============================
        // الحصول على جميع سجلات الإعارة (Paged)
        // ============================
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
                    return NotFound(new { success = false, message = "No borrow records found" });

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
                return StatusCode(500, new { success = false, message = "Unexpected error occurred while fetching borrow records", details = ex.Message });
            }
        }

        // =====================================
        // الحصول على سجل إعارة بواسطة المعرف ID
        // =====================================
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
                    return NotFound(new { success = false, message = $"Borrow record {id} not found" });

                return Ok(new { success = true, data = borrowRecord });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching borrow record {RecordId}", id);
                return StatusCode(500, new { success = false, message = $"Unexpected error while fetching borrow record {id}", details = ex.Message });
            }
        }

        // ============================
        // إنشاء سجل إعارة جديد
        // ============================
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
                    message = "Validation failed",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            try
            {
                var borrowRecord = await _service.AddAsync(dto, ct);
                return CreatedAtAction(nameof(GetById), new { id = borrowRecord.Id }, new
                {
                    success = true,
                    message = "Borrow record created successfully",
                    id = borrowRecord.Id
                });
            }
            catch (ArgumentException ex) // إدخال غير صالح
            {
                _logger.LogWarning(ex, "Create validation error");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex) // تعارض (لا يوجد نسخ متاحة / لديه استعارة فعّالة ...)
            {
                _logger.LogWarning(ex, "Create conflict");
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating borrow record");
                return StatusCode(500, new { success = false, message = "Unexpected error", details = ex.Message });
            }
        }

        // ============================
        // تحديث سجل إعارة موجود
        // ============================
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
                    message = "Validation failed",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            try
            {
                var updated = await _service.UpdateAsync(id, dto, ct);
                if (!updated)
                    return NotFound(new { success = false, message = $"Borrow record {id} not found" });

                return Ok(new { success = true, message = $"Borrow record {id} updated successfully" });
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
                return StatusCode(500, new { success = false, message = $"Unexpected error while updating borrow record {id}", details = ex.Message });
            }
        }

        // ============================
        // حذف سجل إعارة
        // ============================
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
                    return NotFound(new { success = false, message = $"Borrow record {id} not found" });

                return Ok(new { success = true, message = $"Borrow record {id} deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting borrow record {RecordId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Unexpected error while deleting borrow record {id}",
                    details = ex.Message
                });
            }
        }

        // ============================
        // إرجاع كتاب (تعيين ReturnedDate)
        // ============================
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
                    return NotFound(new { success = false, message = $"Borrow record {id} not found" });

                return Ok(new { success = true, message = "Book returned successfully." });
            }
            catch (InvalidOperationException ex) // Already returned
            {
                _logger.LogWarning(ex, "Return conflict for {RecordId}", id);
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error returning borrow record {RecordId}", id);
                return StatusCode(500, new { success = false, message = "Unexpected error while returning book", details = ex.Message });
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

                // لاحظنا: object? وليس object
                List<(string Header, Func<ExportRow, object?>)> headers = new()
        {
            ("ID",            x => x.Id),
            ("Member ID",     x => x.MemberId),
            ("Member Name",   x => x.MemberName ?? ""),
            ("Book ID",       x => x.BookId),
            ("Book Title",    x => x.BookTitle ?? ""),
            ("Borrowed Date", x => x.BorrowedDate),  // نخلي الـ formatter في ExportToExcel عبر dateFormat
            ("Due Date",      x => x.DueDate),
            ("Returned Date", x => x.ReturnedDate),
            ("Status",        x => x.Status),
            ("Overdue Days",  x => x.OverdueDays)
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
