using Data.Services;
using Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PermissionsController : ControllerBase
    {
        private readonly PermissionService _permissionService;

        public PermissionsController(PermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        // =========================
        // GET /permissions
        // =========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllPermissions()
        {
            try
            {
                var permissions = await _permissionService.GetAllPermissionsAsync();
                return Ok(new
                {
                    success = true,
                    data = permissions.Select(p => new { p.Id, p.Name })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ غير متوقع أثناء جلب جميع الصلاحيات",
                    details = ex.Message
                });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPermissionById(int id)
        {
            try
            {
                var perm = await _permissionService.GetPermissionByIdAsync(id);
                if (perm == null)
                    return NotFound(new { success = false, message = $"الصلاحية بالمعرف {id} غير موجودة" });

                return Ok(new { success = true, data = new { perm.Id, perm.Name } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ===== POST /permissions =====
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreatePermission([FromBody] PermissionCreateDto dto)
        {
            try
            {
                var perm = await _permissionService.CreatePermissionAsync(dto.Name);
                return Ok(new { success = true, data = new { perm.Id, perm.Name } });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ===== PUT /permissions/{id} =====
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePermission(int id, [FromBody] PermissionUpdateDto dto)
        {
            try
            {
                var perm = await _permissionService.UpdatePermissionAsync(id, dto.Name);
                return Ok(new { success = true, data = new { perm.Id, perm.Name } });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ===== DELETE /permissions/{id} =====
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePermission(int id)
        {
            try
            {
                var deleted = await _permissionService.DeletePermissionAsync(id);
                if (!deleted)
                    return NotFound(new { success = false, message = $"الصلاحية بالمعرف {id} غير موجودة" });

                return Ok(new { success = true, message = "تم حذف الصلاحية بنجاح" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // =========================
        // GET /permissions/user/{userId}
        // =========================
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUserPermissions(int userId)
        {
            try
            {
                var permissions = await _permissionService.GetUserPermissionsAsync(userId);
                return Ok(new
                {
                    success = true,
                    data = permissions.Select(p => new { p.Id, p.Name })
                });
            }
            catch (KeyNotFoundException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"حدث خطأ غير متوقع أثناء جلب الصلاحيات للمستخدم {userId}",
                    details = ex.Message
                });
            }
        }

        // =========================
        // POST /permissions/user/{userId}/add/{permissionId}
        // =========================
        [HttpPost("user/{userId}/add/{permissionId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddPermissionToUser(int userId, int permissionId)
        {
            try
            {
                var message = await _permissionService.AddPermissionToUserAsync(userId, permissionId);
                if (message.Contains("غير موجود"))
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"حدث خطأ غير متوقع أثناء إضافة الصلاحية {permissionId} للمستخدم {userId}",
                    details = ex.Message
                });
            }
        }

        // =========================
        // DELETE /permissions/user/{userId}/remove/{permissionId}
        // =========================
        [HttpDelete("user/{userId}/remove/{permissionId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemovePermissionFromUser(int userId, int permissionId)
        {
            try
            {
                var message = await _permissionService.RemovePermissionFromUserAsync(userId, permissionId);
                if (message.Contains("غير موجود"))
                    return BadRequest(new { success = false, message });

                return Ok(new { success = true, message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"حدث خطأ غير متوقع أثناء إزالة الصلاحية {permissionId} من المستخدم {userId}",
                    details = ex.Message
                });
            }
        }
    }
}
