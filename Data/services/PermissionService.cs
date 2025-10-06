using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Data.Services
{
    public class PermissionService
    {
        private readonly BankDbContext _context;

        public PermissionService(BankDbContext context)
        {
            _context = context;
        }

        // ===== كل الصلاحيات =====
        public async Task<List<Permission>> GetAllPermissionsAsync()
        {
            return await _context.Permissions.AsNoTracking().ToListAsync();
        }

        // ===== الصلاحيات الفعلية للمستخدم (Role + User - Denied) =====
      public async Task<List<Permission>> GetUserPermissionsAsync(int userId, CancellationToken ct = default)
{
    var user = await _context.Users
        .AsSplitQuery()
        .Include(u => u.Permissions)
        .Include(u => u.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
        .Include(u => u.DeniedPermissions).ThenInclude(dp => dp.Permission)
        .FirstOrDefaultAsync(u => u.Id == userId, ct);

    if (user == null)
        throw new KeyNotFoundException($"User with ID {userId} not found");

    var rolePerms   = user.Role?.RolePermissions.Select(rp => rp.Permission) ?? Enumerable.Empty<Permission>();
    var userPerms   = user.Permissions ?? new List<Permission>();
    var deniedPerms = user.DeniedPermissions?.Select(dp => dp.Permission) ?? Enumerable.Empty<Permission>();

    return rolePerms
        .Union(userPerms, new PermissionIdComparer())
        .Where(p => !deniedPerms.Any(dp => dp.Id == p.Id))
        .Distinct(new PermissionIdComparer())
        .ToList();
}

private sealed class PermissionIdComparer : IEqualityComparer<Permission>
{
    public bool Equals(Permission? x, Permission? y) => x?.Id == y?.Id;
    public int GetHashCode(Permission obj) => obj.Id.GetHashCode();
}

        public async Task<List<Permission>> GetPermissionsByUserIdAsync(int userId) =>
            await GetUserPermissionsAsync(userId);

        public async Task<List<Permission>> GetPermissionsByRoleIdAsync(int roleId)
        {
            return await _context.RolePermissions
                                 .Where(rp => rp.RoleId == roleId)
                                 .Include(rp => rp.Permission)
                                 .Select(rp => rp.Permission)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        // ===== إضافة صلاحية للمستخدم =====
        public async Task<string> AddPermissionToUserAsync(int userId, int permissionId)
        {
            var user = await _context.Users
                .Include(u => u.Permissions)
                .Include(u => u.DeniedPermissions)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            var perm = await _context.Permissions.FindAsync(permissionId);
            if (perm == null)
                throw new KeyNotFoundException($"Permission with ID {permissionId} not found");

            // إذا عنده الصلاحية أصلًا
            if (user.Permissions.Any(p => p.Id == permissionId))
                return $"User '{user.Username}' already has permission '{perm.Name}'";

            // إزالة من Denied إذا موجود
            var denied = user.DeniedPermissions.FirstOrDefault(dp => dp.PermissionId == permissionId);
            if (denied != null)
            {
                _context.UserDeniedPermissions.Remove(denied);
            }
            else
            {
                user.Permissions.Add(perm);
            }

            await _context.SaveChangesAsync();
            return $"Permission '{perm.Name}' assigned to user '{user.Username}' successfully";
        }
        public async Task<Permission?> GetPermissionByIdAsync(int id)
        {
            return await _context.Permissions.FindAsync(id);
        }

        // ===== إنشاء صلاحية جديدة =====
        public async Task<Permission> CreatePermissionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Permission name is required");

            // منع التكرار
            if (await _context.Permissions.AnyAsync(p => p.Name == name))
                throw new InvalidOperationException($"Permission '{name}' already exists");

            var permission = new Permission { Name = name };
            _context.Permissions.Add(permission);
            await _context.SaveChangesAsync();
            return permission;
        }

        // ===== تعديل صلاحية =====
        public async Task<Permission> UpdatePermissionAsync(int id, string name)
        {
            var permission = await _context.Permissions.FindAsync(id);
            if (permission == null)
                throw new KeyNotFoundException($"Permission with ID {id} not found");

            if (await _context.Permissions.AnyAsync(p => p.Name == name && p.Id != id))
                throw new InvalidOperationException($"Permission '{name}' already exists");

            permission.Name = name;
            await _context.SaveChangesAsync();
            return permission;
        }

        // ===== حذف صلاحية =====
        public async Task<bool> DeletePermissionAsync(int id)
        {
            var permission = await _context.Permissions.FindAsync(id);
            if (permission == null) return false;

            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync();
            return true;
        }

        // ===== إزالة صلاحية من المستخدم =====
        public async Task<string> RemovePermissionFromUserAsync(int userId, int permissionId)
        {
            var user = await _context.Users
                .Include(u => u.Permissions)
                .Include(u => u.Role)
                    .ThenInclude(r => r.RolePermissions)
                .Include(u => u.DeniedPermissions)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            var perm = await _context.Permissions.FindAsync(permissionId);
            if (perm == null)
                throw new KeyNotFoundException($"Permission with ID {permissionId} not found");

            bool updated = false;

            // إذا مضافة مباشرة
            if (user.Permissions.Any(p => p.Id == permissionId))
            {
                user.Permissions.Remove(perm);
                updated = true;
            }

            // إذا موروثة من الدور → أضفها للـ Denied
            if (user.Role?.RolePermissions.Any(rp => rp.PermissionId == permissionId) == true &&
                !user.DeniedPermissions.Any(dp => dp.PermissionId == permissionId))
            {
                var denied = new UserDeniedPermission
                {
                    UserId = userId,
                    PermissionId = permissionId
                };
                _context.UserDeniedPermissions.Add(denied);
                updated = true;
            }

            if (updated)
            {
                await _context.SaveChangesAsync();
                return $"Permission '{perm.Name}' removed/denied for user '{user.Username}' successfully";
            }

            return $"User '{user.Username}' does not have permission '{perm.Name}'";
        }
    }
}
