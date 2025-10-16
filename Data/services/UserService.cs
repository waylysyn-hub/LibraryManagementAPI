using Domain.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Data.Services
{
    public class UserService
    {
        private readonly BankDbContext _context;

        public UserService(BankDbContext context)
        {
            _context = context;
        }
        public async Task<Member> GetMemberByEmailAsync(string email, CancellationToken ct = default)
        {
            return await _context.Members
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(m => m.Email.ToLower() == email.ToLower(), ct);
        }
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        public bool VerifyPassword(string enteredPassword, string storedHash)
        {
            var enteredHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(enteredPassword))
            );
            return enteredHash == storedHash;
        }
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                                 .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User> AddUserAsync(string username, string email, string password, int roleId = 3, string? phone = null)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("Username is required");
            if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("Email is required");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                throw new InvalidOperationException("Password must be at least 6 characters");
            if (roleId is < 1 or > 3)
                throw new InvalidOperationException("RoleId must be between 1 and 3");

            var normEmail = email.Trim().ToLowerInvariant();
            var normUser = username.Trim();
            var normPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

            // ✅ Regex للهاتف إذا موجود
            if (!string.IsNullOrEmpty(normPhone) && !Regex.IsMatch(normPhone, @"^(?:09\d{9}|\+963\d{9})$"))
            {
                throw new InvalidOperationException("رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام.");
            }

            // تفريد
            if (await _context.Users.AsNoTracking().AnyAsync(u => u.Email.ToLower() == normEmail))
                throw new DuplicateNameException($"Email '{email}' is already in use.");

            if (await _context.Users.AsNoTracking().AnyAsync(u => u.Username == normUser))
                throw new DuplicateNameException($"Username '{username}' is already in use.");

            if (!string.IsNullOrEmpty(normPhone))
            {
                if (await _context.Users.AsNoTracking().AnyAsync(u => u.Phone == normPhone))
                    throw new DuplicateNameException($"Phone '{normPhone}' is already in use.");
            }

            // إنشاء المستخدم
            var user = new User
            {
                Username = normUser,
                Email = normEmail,
                RoleId = roleId,
                PasswordHash = HashPassword(password),
                CreatedAt = DateTime.UtcNow,
                Phone = normPhone
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

         
            return user;
        }
        public async Task EnsureMemberProfileAsync(
            int userId, string name, string email, string? phone = null, CancellationToken ct = default)
        {
            var user = await _context.Users.FindAsync(new object[] { userId }, ct);
            if (user == null) throw new InvalidOperationException("User not found.");

            var exists = await _context.Members.AsNoTracking()
                .AnyAsync(m => m.Email.ToLower() == email.ToLower(), ct);
            if (exists) return;

            _context.Members.Add(new Member
            {
                UserId = userId,            // لو علاقتك واحد-لواحد موجودة
                Name = name.Trim(),
                Email = email.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim()
            });
            await _context.SaveChangesAsync(ct);
        }
        public Task<bool> ExistsByPhoneAsync(string phone) =>
         _context.Users.AsNoTracking().AnyAsync(u => u.Phone == phone);

        // تحديث بيانات المستخدم + باسورد اختياري
        public async Task<bool> UpdateUserAsync(User user, string? newPassword = null)
        {
            var existing = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existing == null) return false;

            var newUsername = user.Username?.Trim() ?? existing.Username;
            var newEmail = user.Email?.Trim() ?? existing.Email;
            var newPhoneRaw = string.IsNullOrWhiteSpace(user.Phone) ? null : user.Phone.Trim();

            // ✅ Regex للهاتف لو موجود
            if (!string.IsNullOrEmpty(newPhoneRaw) &&
                !Regex.IsMatch(newPhoneRaw, @"^(?:09\d{9}|\+963\d{9})$"))
                throw new InvalidOperationException("رقم الهاتف يجب أن يبدأ بـ 09 أو +963 ثم 9 أرقام.");

            // ✅ تفريد Email / Username / Phone (باستثناء السجل الحالي)
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == newEmail.ToLower() && u.Id != existing.Id))
                throw new InvalidOperationException("Email already in use by another user");

            if (await _context.Users.AnyAsync(u => u.Username == newUsername && u.Id != existing.Id))
                throw new InvalidOperationException("Username already in use by another user");

            if (!string.IsNullOrEmpty(newPhoneRaw) &&
                await _context.Users.AnyAsync(u => u.Phone == newPhoneRaw && u.Id != existing.Id))
                throw new InvalidOperationException("Phone already in use by another user");

            // ✅ تطبيق التعديلات
            existing.Username = newUsername;
            existing.Email = newEmail;
            existing.Phone = newPhoneRaw;

            if (!string.IsNullOrEmpty(newPassword))
                existing.PasswordHash = HashPassword(newPassword);

            await _context.SaveChangesAsync();
            return true;
        }
        // حذف مستخدم
        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        // استعلامات
        public async Task<List<User>> GetAllAsync()
            => await _context.Users.OrderByDescending(u => u.Id).ToListAsync();

        public async Task<User?> GetByIdAsync(int id)
            => await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        public async Task<User?> GetByEmailAsync(string email)
            => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        public async Task<Role?> GetRoleByIdAsync(int roleId)
            => await _context.Roles.FindAsync(roleId);

        public async Task<List<User>> SearchByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new List<User>();
            return await _context.Users
                .Where(u => EF.Functions.Like(u.Username, $"%{name.Trim()}%"))
                .OrderByDescending(u => u.Id)
                .ToListAsync();
        }
    }
}
