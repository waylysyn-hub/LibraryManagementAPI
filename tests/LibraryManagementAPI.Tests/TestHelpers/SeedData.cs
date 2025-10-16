using Data;
using Domain.Entities;

namespace LibraryManagementAPI.Tests.TestHelpers;

public static class SeedData
{
    public static void Seed(BankDbContext ctx)
    {
        // Roles (اختياري لو محتاجها بالخدمة)
        ctx.Roles.AddRange(
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "Employee" },
            new Role { Id = 3, Name = "Member" }
        );

        // Users + Members متوافقين مع تصميمك الحالي
        var uAdmin = new User { Id = 1, Username = "admin", Email = "admin@x.com", RoleId = 1, CreatedAt = DateTime.UtcNow };
        var u1 = new User { Id = 2, Username = "m1", Email = "m1@x.com", RoleId = 3, CreatedAt = DateTime.UtcNow };
        var u2 = new User { Id = 3, Username = "m2", Email = "m2@x.com", RoleId = 3, CreatedAt = DateTime.UtcNow };

        ctx.Users.AddRange(uAdmin, u1, u2);

        ctx.Members.AddRange(
            new Member { Id = 1, UserId = u1.Id, Name = "Ali Ahmad", Email = "m1@x.com" },
            new Member { Id = 2, UserId = u2.Id, Name = "Khaled Saleh", Email = "m2@x.com" }
        );

        ctx.SaveChanges();
    }
}
