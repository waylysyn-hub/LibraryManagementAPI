using Data.Services;
using Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Security.Claims;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembersController : ControllerBase
    {
        private readonly MemberService _service;
        private readonly ILogger<MembersController> _logger;

        public MembersController(MemberService service, ILogger<MembersController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int? GetUserId()
        {
            var c =
                User.FindFirst(ClaimTypes.NameIdentifier) ??
                User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ??
                User.FindFirst("nameid") ??
                User.FindFirst("sub") ??
                User.FindFirst("uid") ??
                User.FindFirst("id");

            if (c is null) return null;
            return int.TryParse(c.Value, out var id) ? id : null;
        }

        // =======================
        // GET /api/members
        // =======================
        [Authorize(Policy = "member.read")]
        [HttpGet]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll([FromQuery] MemberQueryParams qp, CancellationToken ct)
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
                _logger.LogError(ex, "Error fetching members");
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع", details = ex.Message });
            }
        }

        [Authorize(Policy = "member.read")]
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            try
            {
                var member = await _service.GetByIdAsync(id, ct);
                if (member == null)
                    return NotFound(new { success = false, message = $"العضو {id} غير موجود" });

                return Ok(new { success = true, data = member });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching member {MemberId}", id);
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع", details = ex.Message });
            }
        }

        // =======================
        // GET /api/members/me
        // =======================
        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMe(CancellationToken ct)
        {
            var uid = GetUserId();
            if (uid is null) return Unauthorized(new { success = false, message = "توكن غير صالح" });

            try
            {
                var member = await _service.GetByUserIdAsync(uid.Value, ct);
                if (member == null)
                    return NotFound(new { success = false, message = $"العضو الذي يحمل معرف المستخدم {uid} غير موجود" });

                return Ok(new { success = true, data = member });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching me");
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع", details = ex.Message });
            }
        }

        // =======================
        // PUT /api/members/me
        // =======================
        [Authorize(Policy = "member.update")]
        [HttpPut("me")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateMe([FromBody] MemberSelfUpdateDto dto, CancellationToken ct)
        {
            var uid = GetUserId();
            if (uid is null) return Unauthorized(new { success = false, message = "توكن غير صالح" });

            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "البيانات المدخلة غير صالحة.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });

            try
            {
                var ok = await _service.UpdateSelfAsync(uid.Value, dto, ct);
                if (!ok) return NotFound(new { success = false, message = "العضو غير موجود" });
                return Ok(new { success = true, message = "تم تحديث الملف الشخصي بنجاح" });
            }
            catch (DuplicateNameException ex)
            {
                return Conflict(new { success = false, message = ex.Message, field = "email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating my profile");
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع", details = ex.Message });
            }
        }

        // =======================
        // PUT /api/members/{id}
        // =======================
        [HttpPut("{id:int}")]
        [Authorize(Policy = "member.update")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AdminUpdate(int id, [FromBody] MemberAdminUpdateDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "البيانات المدخلة غير صالحة.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            try
            {
                var updated = await _service.AdminUpdateAsync(id, dto, ct);
                if (!updated)
                    return NotFound(new { success = false, message = $"العضو {id} غير موجود" });

                return Ok(new { success = true, message = $"تم تحديث العضو {id} بنجاح" });
            }
            catch (ArgumentException ex) // إذا كان هناك خطأ في التحقق من البيانات (مثل الهاتف أو البريد الإلكتروني)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (DuplicateNameException ex)
            {
                return Conflict(new { success = false, message = ex.Message, field = "email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating member {MemberId}", id);
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع", details = ex.Message });
            }
        }

 
        [Authorize(Policy = "member.delete")]
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try
            {
                var deleted = await _service.DeleteAsync(id, ct);
                if (!deleted)
                    return NotFound(new { success = false, message = $"العضو {id} غير موجود" });

                return Ok(new { success = true, message = $"تم حذف العضو {id} بنجاح" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting member {MemberId}", id);
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع", details = ex.Message });
            }
        }


        [Authorize(Policy = "member.read")]
        [HttpGet("export")]
        [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Export([FromQuery] MemberQueryParams qp, CancellationToken ct)
        {
            try
            {
                var data = await _service.GetForExportAsync(qp, 10000, ct);
                if (data.Count == 0) return NoContent();

                List<(string Header, Func<MemberDto, object?>)> headers =
                new()
                {
                    ("ID",          x => x.Id),
                    ("UserId",      x => x.UserId),
                    ("Name",        x => x.Name),
                    ("Email",       x => x.Email),
                    ("Phone",       x => x.Phone ?? ""),
                    ("Registered",  x => x.RegisteredAt)
                };

                var dateFormat = "yyyy-MM-dd HH:mm";
                var stream = ExcelExportService.ExportToExcel<MemberDto>(data, headers, "Members", dateFormat);

                var fileName = $"members_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                const string ctExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return File(stream.ToArray(), ctExcel, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting members");
                return StatusCode(500, new { success = false, message = "خطأ أثناء إنشاء ملف الإكسل", details = ex.Message });
            }
        }
    }
}
