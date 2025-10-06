using Data.Services;
using Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly BookService _service;
        private readonly ILogger<BooksController> _logger;

        public BooksController(BookService service, ILogger<BooksController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ============================
        // Get all (paged)
        // ============================
        [HttpGet]
        [Authorize(Policy = "book.read")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll([FromQuery] BookQueryParams qp, CancellationToken ct)
        {
            try
            {
                var (items, total) = await _service.GetPagedAsync(qp, ct);

                var page = qp.Page < 1 ? 1 : qp.Page;
                var pageSize = qp.PageSize <= 0 ? 50 : (qp.PageSize > 200 ? 200 : qp.PageSize);

                return Ok(new
                {
                    success = true,
                    message = items.Count == 0 ? "لا توجد نتائج" : "تم الجلب بنجاح",
                    data = items,
                    meta = new
                    {
                        page,
                        pageSize,
                        total,
                        totalPages = (int)Math.Ceiling(total / (double)pageSize),
                        sortBy = qp.SortBy.ToString(),
                        sortDir = qp.SortDir.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching books");
                return StatusCode(500, new { success = false, message = "Unexpected error", details = ex.Message });
            }
        }

        // ============================
        // Get by id
        // ============================
        [Authorize(Policy = "book.read")]
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            try
            {
                var book = await _service.GetByIdAsync(id);
                if (book == null)
                    return NotFound(new { success = false, message = $"Book {id} not found" });

                return Ok(new { success = true, data = book });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching book {BookId}", id);
                return StatusCode(500, new { success = false, message = "Unexpected error", details = ex.Message });
            }
        }

        // ============================
        // Create
        // ============================
        [Authorize(Policy = "book.create")]
        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] BookCreateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "Validation failed",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });

            try
            {
                var book = await _service.AddAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = book.Id },
                    new { success = true, message = "Book created", id = book.Id });
            }
            catch (ArgumentException ex) // إدخال غير صالح (مثلاً سنة/ISBN)
            {
                _logger.LogWarning(ex, "Create validation error");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex) // تعارضات (ISBN مكرر، Title+Author+Year)
            {
                _logger.LogWarning(ex, "Create conflict");
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating book");
                return StatusCode(500, new { success = false, message = "Unexpected error", details = ex.Message });
            }
        }

        // ============================
        // Update
        // ============================
        [Authorize(Policy = "book.update")]
        [HttpPut("{id:int}")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(int id, [FromBody] BookUpdateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "Validation failed",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });

            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                if (!updated)
                    return NotFound(new { success = false, message = $"Book {id} not found" });

                return Ok(new { success = true, message = $"Book {id} updated successfully" });
            }
            catch (ArgumentException ex) // إدخال غير صالح
            {
                _logger.LogWarning(ex, "Update validation error for {BookId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex) // تعارضات
            {
                _logger.LogWarning(ex, "Update conflict for {BookId}", id);
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating book {BookId}", id);
                return StatusCode(500, new { success = false, message = "Unexpected error", details = ex.Message });
            }
        }

        // ============================
        // Delete
        // ============================
        [Authorize(Policy = "book.delete")]
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try
            {
                var deleted = await _service.DeleteAsync(id);
                if (!deleted)
                    return NotFound(new { success = false, message = $"Book {id} not found" });

                return Ok(new { success = true, message = $"Book {id} deleted successfully" });
            }
            catch (InvalidOperationException ex) // عليه سجلات استعارة
            {
                _logger.LogWarning(ex, "Delete conflict for {BookId}", id);
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting book {BookId}", id);
                return StatusCode(500, new { success = false, message = "Unexpected error", details = ex.Message });
            }
        }

        // ============================
        // Export
        // ============================
        [Authorize(Policy = "book.read")]
        [HttpGet("export")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Export([FromQuery] BookQueryParams qp, CancellationToken ct)
        {
            try
            {
                var data = await _service.GetForExportAsync(qp, 10000, ct);
                if (data.Count == 0) return NoContent();

                var headers = new List<(string Header, Func<BookDto, object>)>
                {
                    ("ID",         x => x.Id),
                    ("Title",      x => x.Title),
                    ("Author",     x => x.Author),
                    ("Category",   x => x.Category),
                    ("Year",       x => x.Year),
                    ("Copies",     x => x.CopiesCount),
                    ("Active Borrows", x => x.ActiveBorrowCount),
                    ("Available",       x => x.AvailableCopies),
                    ("ISBN",       x => x.ISBN),
                };

                var stream = ExcelExportService.ExportToExcel(data, headers, "Books");
                var fileName = $"books_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                const string ctExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return File(stream.ToArray(), ctExcel, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting books");
                return StatusCode(500, new { success = false, message = "خطأ أثناء إنشاء ملف الإكسل", details = ex.Message });
            }
        }
    }
}
