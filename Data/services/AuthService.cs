using Domain.DTOs;
using Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace Data.Services
{
    public class AuthService
    {
        private readonly BankDbContext _context;
        private readonly JwtService _jwtService;

        public AuthService(BankDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        /// <summary>
        /// Verifies a plaintext password against a stored SHA256 Base64 hash.
        /// </summary>
        private static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
                return false;

            using var sha256 = SHA256.Create();
            var computed = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return computed == hash;
        }

        /// <summary>
        /// Registers a standard user as Member (RoleId=3) and creates a Member profile within a DB transaction.
        /// </summary>
        public async Task<User> RegisterMemberAsync(UserRegisterDto dto, CancellationToken ct = default)
        {
            // تطبيع المدخلات
            var email = dto.Email?.Trim().ToLowerInvariant() ?? throw new InvalidOperationException("Email is required.");
            var username = dto.Username?.Trim() ?? throw new InvalidOperationException("Username is required.");
            var name = dto.Name?.Trim() ?? throw new InvalidOperationException("Name is required.");
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
                throw new InvalidOperationException("Password must be at least 6 characters.");

            // فحوصات فريدة (case-insensitive للإيميل)
            if (await _context.Users.AsNoTracking().AnyAsync(u => u.Email.ToLower() == email, ct))
                throw new DuplicateNameException($"Email '{dto.Email}' is already in use.");
            if (await _context.Users.AsNoTracking().AnyAsync(u => u.Username == username, ct))
                throw new DuplicateNameException($"Username '{dto.Username}' is already in use.");

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // 1) إنشاء User بدور Member (RoleId=3)
                var passHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(dto.Password)));

                var user = new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = passHash,
                    RoleId = 3, // Member
                    CreatedAt = DateTime.UtcNow
                };

                // 2) إنشاء Member مربوط على UserId (واحد-لواحد)
                _context.Users.Add(user);
                await _context.SaveChangesAsync(ct);

                var member = new Member
                {
                    UserId = user.Id,          // 👈 هذا مهم
                    Name = dto.Name.Trim(),
                    Email = email
                };
                _context.Members.Add(member);


                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return user;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2627 || sql.Number == 2601))
            {
                // Unique index violation (Email/Username/Member.UserId/Member.Email)
                await tx.RollbackAsync(ct);
                throw new DuplicateNameException("Email or Username already exists.", ex);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        /// <summary>
        /// Authenticates a user, computes effective permissions: Role ∪ User − Denied, and issues a JWT.
        /// </summary>
        public async Task<LoginResultDto?> LoginAsync(string email, string password, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    return new LoginResultDto { Token = null!, RoleName = null!, Permissions = new List<string>() };

                var normalizedEmail = email.Trim().ToLowerInvariant();

                var user = await _context.Users
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(u => u.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                    .Include(u => u.Permissions) // user-level grants
                    .Include(u => u.DeniedPermissions)
                        .ThenInclude(dp => dp.Permission)
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, ct);

                if (user == null)
                    return new LoginResultDto { Token = null!, RoleName = null!, Permissions = new List<string>() };

                if (!VerifyPassword(password, user.PasswordHash))
                    return new LoginResultDto { Token = null!, RoleName = null!, Permissions = new List<string>() };

                var roleName = user.Role?.Name ?? "No role assigned";

                var rolePermissions = user.Role?.RolePermissions.Select(rp => rp.Permission).ToList()
                                      ?? new List<Permission>();
                var userPermissions = user.Permissions?.ToList() ?? new List<Permission>();
                var deniedPermissions = user.DeniedPermissions?.Select(dp => dp.Permission).ToList()
                                         ?? new List<Permission>();

                var finalPermissions = rolePermissions
                    .Union(userPermissions, new PermissionIdComparer())
                    .Where(p => !deniedPermissions.Any(dp => dp.Id == p.Id))
                    .Select(p => p.Name)
                    .Distinct()
                    .ToList();

                var token = _jwtService.GenerateToken(user, finalPermissions);

                return new LoginResultDto
                {
                    Token = token,
                    RoleName = roleName,
                    Permissions = finalPermissions
                };
            }
            catch
            {
                // Controller will handle a null result as 500
                return null;
            }
        }

        private sealed class PermissionIdComparer : IEqualityComparer<Permission>
        {
            public bool Equals(Permission? x, Permission? y) => x?.Id == y?.Id;
            public int GetHashCode(Permission obj) => obj.Id.GetHashCode();
        }
    }
}
