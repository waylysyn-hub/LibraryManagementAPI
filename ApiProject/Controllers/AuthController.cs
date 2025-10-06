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
        /// Login with email & password. Returns JWT and effective permissions.
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
                return BadRequest(new { success = false, message = "Request body is missing." });

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { success = false, message = "Email and password are required." });

            try
            {
                var result = await _authService.LoginAsync(dto.Email, dto.Password, ct);

                if (result is null)
                {
                    _logger.LogError("LoginAsync returned null (unexpected).");
                    return StatusCode(500, new { success = false, message = "Internal server error while processing login." });
                }

                if (string.IsNullOrEmpty(result.Token))
                    return Unauthorized(new { success = false, message = "Invalid email or password." });

                var message = string.IsNullOrEmpty(result.RoleName)
                    ? "Login successful but user has no role assigned."
                    : (result.Permissions.Count == 0
                        ? $"Login successful. You are logged in as '{result.RoleName}', but you have no permissions assigned yet."
                        : $"Login successful. You are logged in as '{result.RoleName}'.");

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
                _logger.LogWarning("Login cancelled by client.");
                return StatusCode(499, new { success = false, message = "Client closed request." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login.");
                return StatusCode(500, new { success = false, message = $"Unexpected error during login: {ex.Message}" });
            }
        }

        /// <summary>
        /// Logout by blacklisting the current JWT (token will not be accepted until it expires).
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
                    return BadRequest(new { success = false, message = "Authorization header with Bearer token is required." });

                var token = authHeader.Substring("Bearer ".Length).Trim();
                if (string.IsNullOrWhiteSpace(token))
                    return BadRequest(new { success = false, message = "Token not found." });

                // Validate token signature without lifetime check (we only need claims and ValidTo)
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
                        ?? throw new SecurityTokenException("Invalid token format.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid token or signature on logout.");
                    return BadRequest(new { success = false, message = "Invalid token or signature." });
                }

                // 👇 استدعاءات توافق تواقيع BlacklistService الحالية
                if (await _blacklistService.IsTokenRevokedAsync(token))
                    return NotFound(new { success = false, message = "Token has already been revoked." });

                await _blacklistService.AddToBlacklistAsync(token, validatedToken.ValidTo);

                return Ok(new
                {
                    success = true,
                    message = "Logout successful. Token is now invalidated.",
                    revokedUntil = validatedToken.ValidTo
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Logout cancelled by client.");
                return StatusCode(499, new { success = false, message = "Client closed request." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during logout.");
                return StatusCode(500, new { success = false, message = $"Unexpected error during logout: {ex.Message}" });
            }
        }
    }
}
