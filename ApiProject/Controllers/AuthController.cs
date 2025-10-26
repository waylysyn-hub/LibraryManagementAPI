using Data.Services;
using Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace ApiProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly BlacklistService _blacklistService;
        private readonly JwtService _jwtService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            AuthService authService,
            BlacklistService blacklistService,
            JwtService jwtService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _blacklistService = blacklistService;
            _jwtService = jwtService;
            _logger = logger;
        }

        /// <summary>
        /// تسجيل الدخول بإيميل وكلمة مرور. يُرجع JWT مع الأذونات الفعالة.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { success = false, message = "هيكل الطلب مفقود." });

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { success = false, message = "البريد الإلكتروني وكلمة المرور مطلوبان." });

            try
            {
                var result = await _authService.LoginAsync(dto.Email, dto.Password, ct);

                if (result is null)
                {
                    _logger.LogError("LoginAsync أعادت null (غير متوقع).");
                    return StatusCode(500, new { success = false, message = "حدث خطأ داخلي أثناء معالجة تسجيل الدخول." });
                }

                if (string.IsNullOrEmpty(result.Token))
                    return Unauthorized(new { success = false, message = "بيانات الدخول غير صحيحة." });

                var message = string.IsNullOrEmpty(result.RoleName)
                    ? "تم تسجيل الدخول، لكن لا يوجد دور مخصّص للمستخدم."
                    : (result.Permissions.Count == 0
                        ? $"تم تسجيل الدخول كـ '{result.RoleName}'، لكن لا توجد أذونات مخصّصة بعد."
                        : $"تم تسجيل الدخول كـ '{result.RoleName}'.");

                return Ok(new
                {
                    success = true,
                    message,
                    data = new
                    {
                        token = result.Token,
                        role = result.RoleName,
                        permissions = result.Permissions
                    }
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("تم إلغاء عملية تسجيل الدخول من العميل.");
                return StatusCode(499, new { success = false, message = "تم إنهاء الطلب من جهة العميل." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء تسجيل الدخول.");
                return StatusCode(500, new { success = false, message = $"خطأ غير متوقع أثناء تسجيل الدخول: {ex.Message}" });
            }
        }

        /// <summary>
        /// تسجيل الخروج بوضع الـ JWT الحالي في القائمة السوداء حتى انتهاء صلاحيته.
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            try
            {
                var authHeader = Request.Headers.Authorization.ToString();
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
                    return BadRequest(new { success = false, message = "يلزم ترويسة Authorization مع رمز Bearer." });

                var token = authHeader.Substring("Bearer ".Length).Trim();
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { success = false, message = "الرمز مفقود." });

                // التحقق من التوقيع بدون التحقق من مدة الحياة (نحتاج الادعاءات ووقت الانتهاء فقط)
                JwtSecurityToken validatedToken;
                try
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    tokenHandler.ValidateToken(
                        token,
                        new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtService.Secret)),
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            ValidateLifetime = false
                        },
                        out var tmp);

                    validatedToken = tmp as JwtSecurityToken
                        ?? throw new SecurityTokenException("صيغة الرمز غير صحيحة.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "رمز غير صالح/توقيع خاطئ أثناء تسجيل الخروج.");
                    return BadRequest(new { success = false, message = "الرمز غير صالح أو التوقيع غير صحيح." });
                }

                if (await _blacklistService.IsTokenRevokedAsync(token))
                    return NotFound(new { success = false, message = "تم إبطال هذا الرمز مسبقًا." });

                await _blacklistService.AddToBlacklistAsync(token, validatedToken.ValidTo);

                return Ok(new
                {
                    success = true,
                    message = "تم تسجيل الخروج. أصبح الرمز لاغيًا.",
                    revokedUntil = validatedToken.ValidTo
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("تم إلغاء عملية تسجيل الخروج من العميل.");
                return StatusCode(499, new { success = false, message = "تم إنهاء الطلب من جهة العميل." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع أثناء تسجيل الخروج.");
                return StatusCode(500, new { success = false, message = $"خطأ غير متوقع أثناء تسجيل الخروج: {ex.Message}" });
            }
        }
    }
}
