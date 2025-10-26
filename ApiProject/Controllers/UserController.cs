using Data.Services;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text.RegularExpressions;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly PermissionService _permissionService;
        private readonly AuthService _authService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            UserService userService,
            PermissionService permissionService,
            AuthService authService,
            ILogger<UsersController> logger)
        {
            _userService = userService;
            _permissionService = permissionService;
            _authService = authService;
            _logger = logger;
        }


        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var users = await _userService.GetAllAsync();
            if (users.Count == 0)
                return Ok(new { success = true, message = "لا توجد مستخدمين", data = Array.Empty<object>() });

            return Ok(new
            {
                success = true,
                count = users.Count,
                data = users.Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Phone,
                    u.RoleId,
                    u.CreatedAt
                })
            });
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "معرف المستخدم غير صالح. يجب أن يكون أكبر من 0." });

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = $"المستخدم بالمعرف {id} غير موجود." });

            var permissions = await _permissionService.GetUserPermissionsAsync(id);

            return Ok(new
            {
                success = true,
                data = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.Phone,
                    user.RoleId,
                    user.CreatedAt,
                    Permissions = permissions.Select(p => new { p.Id, p.Name })
                }
            });
        }


        [HttpPost("public-register")]
        [AllowAnonymous]
        public async Task<IActionResult> PublicRegister([FromBody] UserRegisterDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "البيانات المدخلة غير صالحة. يرجى التحقق من الحقول.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            if (dto.Password != dto.ConfirmPassword)
            {
                return BadRequest(new { success = false, message = "كلمة المرور وكلمة المرور المؤكدة غير متطابقتين." });
            }

            var phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            if (!string.IsNullOrEmpty(phone) && !Regex.IsMatch(phone, @"^(?:09\d{9}|\+963\d{9})$"))
                return BadRequest(new { success = false, field = "Phone", message = "رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام." });

            try
            {
                // التحقق من وجود البريد الإلكتروني في جدول الأعضاء فقط
                var existingMemberByEmail = await _userService.GetMemberByEmailAsync(dto.Email);
                if (existingMemberByEmail != null)
                {
                    return BadRequest(new { success = false, message = "البريد الإلكتروني مستخدم بالفعل. يرجى اختيار بريد آخر." });
                }

                // التحقق من وجود رقم الهاتف في جدول الأعضاء فقط
                if (!string.IsNullOrEmpty(phone) && await _userService.ExistsByPhoneAsync(phone))
                {
                    return Conflict(new { success = false, field = "Phone", message = "رقم الهاتف مستخدم مسبقاً." });
                }

                // تسجيل العضو
                var user = await _authService.RegisterMemberAsync(dto, ct); // تأكد من تمرير Phone للـ User

                var role = await _userService.GetRoleByIdAsync(user.RoleId);
                var rolePerms = await _permissionService.GetPermissionsByRoleIdAsync(user.RoleId);

                return StatusCode(201, new
                {
                    success = true,
                    message = "تم تسجيل المستخدم بنجاح.",
                    data = new
                    {
                        user.Id,
                        user.Username,
                        user.Email,
                        RoleId = 3,
                        RoleName =  "Member",
                        Permissions = rolePerms.Select(p => new { p.Id, p.Name })
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "PublicRegister conflict");
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (DuplicateNameException ex)
            {
                var field =
                    ex.Message.Contains("Email", StringComparison.OrdinalIgnoreCase) ? "email" :
                    ex.Message.Contains("Username", StringComparison.OrdinalIgnoreCase) ? "username" :
                    ex.Message.Contains("Phone", StringComparison.OrdinalIgnoreCase) ? "phone" : null;

                return Conflict(new { success = false, message = ex.Message, field });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PublicRegister error");
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع في الخادم." });
            }
        }

        [HttpPost("admin-register")]
        [Authorize(Roles = "Admin")]
        [Consumes("application/x-www-form-urlencoded")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AdminRegister([FromForm] AdminCreateUserDto dto, CancellationToken ct)
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

            if (dto.Password != dto.ConfirmPassword)
                return BadRequest(new { success = false, message = "كلمة المرور وكلمة المرور المؤكدة غير متطابقتين." });

            // ✅ تحقّق صيغة الهاتف (اختياريًا إذا مُرسل)
            var phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            if (!string.IsNullOrEmpty(phone) && !Regex.IsMatch(phone, @"^(?:09\d{9}|\+963\d{9})$"))
                return BadRequest(new { success = false, field = "Phone", message = "رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام." });

            var roleId = (int)dto.Role; // Admin=1, Employee=2

            try
            {
                var user = await _userService.AddUserAsync(dto.Username, dto.Email, dto.Password, roleId, phone);

                // لا ننشئ Member هنا

                var role = await _userService.GetRoleByIdAsync(roleId);
                var rolePermissions = await _permissionService.GetPermissionsByRoleIdAsync(roleId);

                return CreatedAtAction(nameof(GetById), new { id = user.Id }, new
                {
                    success = true,
                    message = "تم تسجيل المستخدم بنجاح.",
                    data = new
                    {
                        user.Id,
                        user.Username,
                        user.Email,
                        RoleId = roleId,
                        RoleName = role?.Name ?? dto.Role.ToString(),
                        Permissions = rolePermissions.Select(p => new { p.Id, p.Name })
                    }
                });
            }
            catch (DuplicateNameException ex)
            {
                var field =
                    ex.Message.Contains("Email", StringComparison.OrdinalIgnoreCase) ? "email" :
                    ex.Message.Contains("Username", StringComparison.OrdinalIgnoreCase) ? "username" :
                    ex.Message.Contains("Phone", StringComparison.OrdinalIgnoreCase) ? "phone" : null;

                return Conflict(new { success = false, message = ex.Message, field });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2627 || sql.Number == 2601))
            {
                return Conflict(new
                {
                    success = false,
                    message = "البريد الإلكتروني، اسم المستخدم أو رقم الهاتف موجود مسبقاً.",
                    detail = ex.InnerException.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while admin-register");
                return StatusCode(500, new { success = false, message = "خطأ غير متوقع في الخادم." });
            }
        }

        // ===========================
        // Update Password
        // ===========================
        [HttpPut("{id:int}/password")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePassword(int id, [FromBody] UpdatePasswordDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState); // 👈 يعيد errors مع أسماء الحقول

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = $"المستخدم بالمعرف {id} غير موجود." });

            var currentUserEmail = User.Identity?.Name;
            if (currentUserEmail != user.Email && !User.IsInRole("Admin"))
                return Forbid("يمكنك تغيير كلمة مرورك فقط.");

      if (!_userService.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
    return BadRequest(new {
        success = false,
        message = "كلمة المرور الحالية غير صحيحة.",
        field = "CurrentPassword" // يفيد الواجهة تلون الحقل
    });

            var updated = await _userService.UpdateUserAsync(user, dto.NewPassword);
            if (!updated)
                return BadRequest(new { success = false, message = "فشل تحديث كلمة المرور." });

            return Ok(new { success = true, message = "تم تحديث كلمة المرور بنجاح." });
        }

        // ===========================
        // Update User Info
        // ===========================
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDto dto, CancellationToken ct)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "معرف المستخدم غير صالح. يجب أن يكون أكبر من 0." });

            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "البيانات المدخلة غير صالحة. يرجى التحقق من الحقول.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });

            try
            {
                var user = new User
                {
                    Id = id,
                    Username = dto.Username,
                    Email = dto.Email,
                    Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim()
                };

                var updated = await _userService.UpdateUserAsync(user);
                if (!updated)
                    return NotFound(new { success = false, message = $"المستخدم بالمعرف {id} غير موجود." });

                return Ok(new { success = true, message = "تم تحديث المستخدم بنجاح." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2627 || sql.Number == 2601))
            {
                return Conflict(new { success = false, message = "البريد الإلكتروني، اسم المستخدم أو رقم الهاتف موجود مسبقاً.", detail = ex.InnerException.Message });
            }
        }

        // ===========================
        // Delete User
        // ===========================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "معرف المستخدم غير صالح. يجب أن يكون أكبر من 0." });

            var deleted = await _userService.DeleteUserAsync(id);
            if (!deleted)
                return NotFound(new { success = false, message = $"المستخدم بالمعرف {id} غير موجود." });

            return Ok(new { success = true, message = "تم حذف المستخدم بنجاح." });
        }
    }
}
