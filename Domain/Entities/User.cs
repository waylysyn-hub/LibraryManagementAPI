using Domain.Entities;
using System.Data;

namespace Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🔹 العلاقة مع Member (واحد لواحد)
        public Member? Member { get; set; }
        public List<UserDeniedPermission> DeniedPermissions { get; set; } = new();
        public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    }
}
