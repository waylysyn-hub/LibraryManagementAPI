using Data.Services;
using Domain.DTOs;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

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

        // ===========================
        // Get All Users
        // ===========================
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var users = await _userService.GetAllAsync();
            if (users.Count == 0)
                return Ok(new { success = true, message = "No users found", data = Array.Empty<object>() });

            return Ok(new
            {
                success = true,
                count = users.Count,
                data = users.Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.RoleId,
                    u.CreatedAt
                })
            });
        }

        // ===========================
        // Get User by Id
        // ===========================
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid user id. Id must be greater than 0." });

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            var permissions = await _permissionService.GetUserPermissionsAsync(id);

            return Ok(new
            {
                success = true,
                data = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.RoleId,
                    user.CreatedAt,
                    Permissions = permissions.Select(p => new { p.Id, p.Name })
                }
            });
        }

        // ===========================
        // Admin Register (Form + Dropdown للـ Role) — Admin/Employee فقط
        // ===========================
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
                    message = "Invalid input data.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            if (dto.Password != dto.ConfirmPassword)
                return BadRequest(new { success = false, message = "Password and Confirm Password do not match." });

            var roleId = (int)dto.Role; // Admin=1, Employee=2

            try
            {
                var user = await _userService.AddUserAsync(dto.Username, dto.Email, dto.Password, roleId);

                // لا ننشئ Member هنا

                var role = await _userService.GetRoleByIdAsync(roleId);
                var rolePermissions = await _permissionService.GetPermissionsByRoleIdAsync(roleId);

                return CreatedAtAction(nameof(GetById), new { id = user.Id }, new
                {
                    success = true,
                    message = "User registered successfully.",
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
                // رُميّت من الخدمة لما يكون الإيميل/اليوزرنيم مستعمل
                var field = ex.Message.Contains("Email", StringComparison.OrdinalIgnoreCase) ? "email" :
                            ex.Message.Contains("Username", StringComparison.OrdinalIgnoreCase) ? "username" : null;

                return Conflict(new { success = false, message = ex.Message, field });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
                                               (sql.Number == 2627 || sql.Number == 2601))
            {
                // 2627: Primary/Unique key violation, 2601: duplicate key
                // في حال ما قدرنا نمسك التضارب مسبقًا
                return Conflict(new
                {
                    success = false,
                    message = "Email or Username already exists.",
                    detail = ex.InnerException.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                // أخطاء فاليديشن أُخرى من الخدمة
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while admin-register");
                return StatusCode(500, new { success = false, message = "Unexpected server error." });
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
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid input data.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            // التحقق من الصلاحيات: فقط Admin أو صاحب الحساب نفسه
            var currentUserEmail = User.Identity?.Name;
            if (currentUserEmail != user.Email && !User.IsInRole("Admin"))
                return Forbid("You can only change your own password.");

            if (!_userService.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
                return Unauthorized(new { success = false, message = "Current password is incorrect." });

            var updated = await _userService.UpdateUserAsync(user, dto.NewPassword);
            if (!updated)
                return BadRequest(new { success = false, message = "Password update failed." });

            return Ok(new { success = true, message = "Password updated successfully." });
        }

        // ===========================
        // Update User Info
        // ===========================
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDto dto, CancellationToken ct)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid user id. Id must be greater than 0." });

            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid input data. Please check the fields.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });

            var existingUser = await _userService.GetByEmailAsync(dto.Email);
            if (existingUser != null && existingUser.Id != id)
                return Conflict(new { success = false, message = $"Email '{dto.Email}' is already used by another user." });

            var user = new User { Id = id, Username = dto.Username, Email = dto.Email };
            var updated = await _userService.UpdateUserAsync(user);
            if (!updated)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            return Ok(new { success = true, message = "User updated successfully." });
        }

        // ===========================
        // Update User Role
        // ===========================
        [HttpPut("{id:int}/role")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] int newRoleId, CancellationToken ct)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid user id. Id must be greater than 0." });

            if (newRoleId is < 1 or > 3)
                return BadRequest(new { success = false, message = "RoleId must be 1 (Admin), 2 (Employee), or 3 (Member)." });

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            user.RoleId = newRoleId;
            await _userService.UpdateUserAsync(user);

            var role = await _userService.GetRoleByIdAsync(newRoleId);
            var rolePermissions = await _permissionService.GetPermissionsByRoleIdAsync(newRoleId);

            return Ok(new
            {
                success = true,
                message = $"User role updated to {newRoleId} successfully.",
                RoleId = newRoleId,
                RoleName = role?.Name ?? "Unknown",
                Permissions = rolePermissions.Select(p => new { p.Id, p.Name })
            });
        }

        // ===========================
        // Delete User
        // ===========================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid user id. Id must be greater than 0." });

            var deleted = await _userService.DeleteUserAsync(id);
            if (!deleted)
                return NotFound(new { success = false, message = $"User with ID {id} not found." });

            return Ok(new { success = true, message = "User deleted successfully." });
        }
    }
}
