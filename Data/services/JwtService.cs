using Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Data.Services
{
    public class JwtService
    {
        private readonly string _secret;
        private readonly string _issuer;
        private readonly string _audience;

        public JwtService(string secret, string issuer, string audience)
        {
            _secret = secret;
            _issuer = issuer;
            _audience = audience;
        }

        public string Secret => _secret; // للوصول من الخارج (مثلاً للـ logout)

        public string GenerateToken(User user, List<string> permissions)
        {
            if (user is null) throw new ArgumentNullException(nameof(user));

            var nowUtc = DateTime.UtcNow;

            // نظّف الصلاحيات وامنع التكرار
            var distinctPerms = (permissions ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var roleName = user.Role?.Name ?? "User";

            var claims = new List<Claim>
    {
        // هوية المستخدم
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email ?? string.Empty),

        // معلومات الدور
        new Claim("role_id", user.RoleId.ToString()),
        new Claim("role_name", roleName),

        // لازم لعمل [Authorize(Roles="...")]
        new Claim(ClaimTypes.Role, roleName),

        // ميتاداتا للتتبع والـ blacklist
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        new Claim(JwtRegisteredClaimNames.Iat,
                  new DateTimeOffset(nowUtc).ToUnixTimeSeconds().ToString(),
                  ClaimValueTypes.Integer64)
    };

            // أضف كل صلاحية كـ claim مستقل
            foreach (var perm in distinctPerms)
                claims.Add(new Claim("permission", perm));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                notBefore: nowUtc,
                expires: nowUtc.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
