using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;

namespace Data
{
    public class BankDbContext : DbContext
    {
        public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }
        public DbSet<Book> Books { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<BorrowRecord> BorrowRecords { get; set; }
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<RevokedToken> RevokedTokens { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<UserDeniedPermission> UserDeniedPermissions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
          
            modelBuilder.Entity<Book>()
                .HasIndex(b => new { b.Title, b.Author });

            modelBuilder.Entity<Member>()
                .HasIndex(m => m.Email)
                .IsUnique();

            modelBuilder.Entity<Member>()
                .Property(m => m.RegisteredAt)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            modelBuilder.Entity<User>()
                .HasOne(u => u.Member)
                .WithOne(m => m.User)
                .HasForeignKey<Member>(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Member>()
                .HasIndex(m => m.UserId).IsUnique();

            // (اختياري لكن مُستحسن)
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<Member>().HasIndex(m => m.Email).IsUnique();


            // (اختياري) قيود طول/إلزامية
            modelBuilder.Entity<Member>().Property(m => m.Name).HasMaxLength(150).IsRequired();
            modelBuilder.Entity<Member>().Property(m => m.Email).HasMaxLength(200).IsRequired();
            modelBuilder.Entity<User>().Property(u => u.Email).HasMaxLength(200).IsRequired();
            modelBuilder.Entity<User>().Property(u => u.Username).HasMaxLength(100).IsRequired();
            // User -> Role (many-to-1)
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId);

            // RolePermission (many-to-many Role <-> Permission)
            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            // User-Permission (many-to-many User <-> Permission)
            modelBuilder.Entity<User>()
                .HasMany(u => u.Permissions)
                .WithMany()
                .UsingEntity(j => j.ToTable("UserPermissions"));

            // UserDeniedPermissions
            modelBuilder.Entity<UserDeniedPermission>()
                .HasKey(dp => new { dp.UserId, dp.PermissionId });
            modelBuilder.Entity<UserDeniedPermission>()
                .HasOne(dp => dp.User)
                .WithMany(u => u.DeniedPermissions)
                .HasForeignKey(dp => dp.UserId);
            modelBuilder.Entity<UserDeniedPermission>()
                .HasOne(dp => dp.Permission)
                .WithMany()
                .HasForeignKey(dp => dp.PermissionId);


            // Seed Admin User
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "wael",
                    Email = "admin@example.com",
                    PasswordHash = "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=", // SHA256("123456")
                    RoleId = 1,
                    CreatedAt = new DateTime(2025, 9, 20, 0, 0, 0)
                }
            );

            // Seed Permissions
            // Book -> BorrowRecords (1-to-many)
            modelBuilder.Entity<Book>()
                .HasMany(b => b.BorrowRecords)
                .WithOne(br => br.Book)
                .HasForeignKey(br => br.BookId);
            // تعريف العلاقات الأخرى (كما تم شرحه سابقًا)
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

       
            // إضافة البيانات الأولية (Seed Data) للأدوار والصلاحيات
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Employee" },
                new Role { Id = 3, Name = "Member" }
            );

            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = 1, Name = "book.read" },
                new Permission { Id = 2, Name = "member.add" },
                new Permission { Id = 3, Name = "member.update" },
                new Permission { Id = 4, Name = "member.delete" },
                new Permission { Id = 5, Name = "book.create" },
                new Permission { Id = 6, Name = "book.update" },
                new Permission { Id = 7, Name = "book.delete" },
                new Permission { Id = 8, Name = "member.read" },
                new Permission { Id = 9, Name = "borrow.read" },
                new Permission { Id = 10, Name = "borrow.update" },
                new Permission { Id = 11, Name = "borrow.delete" },
                new Permission { Id = 12, Name = "borrow.create" }
            );

            modelBuilder.Entity<RolePermission>().HasData(
                new RolePermission { RoleId = 1, PermissionId = 1 }, // Admin - book.read
                new RolePermission { RoleId = 1, PermissionId = 2 }, // Admin - member.add
                new RolePermission { RoleId = 1, PermissionId = 3 }, // Admin - member.update
                new RolePermission { RoleId = 1, PermissionId = 4 }, // Admin - member.delete
                new RolePermission { RoleId = 1, PermissionId = 5 }, // Admin - book.create
                new RolePermission { RoleId = 1, PermissionId = 6 }, // Admin - book.update
                new RolePermission { RoleId = 1, PermissionId = 7 }, // Admin - book.delete
                new RolePermission { RoleId = 1, PermissionId = 8 },
                new RolePermission { RoleId = 1, PermissionId = 9 },
                new RolePermission { RoleId = 1, PermissionId = 10 },
                new RolePermission { RoleId = 1, PermissionId = 11 },
                new RolePermission { RoleId = 1, PermissionId = 12 },

                new RolePermission { RoleId = 2, PermissionId = 1 }, // Employee - book.read
                new RolePermission { RoleId = 2, PermissionId = 2 }, // Employee - member.add
                new RolePermission { RoleId = 2, PermissionId = 3 }, // Employee - member.update
                new RolePermission { RoleId = 2, PermissionId = 5 }, // Employee - book.create
                new RolePermission { RoleId = 2, PermissionId = 6 }, // Employee - book.update
                 new RolePermission { RoleId = 2, PermissionId = 8 },

                new RolePermission { RoleId = 3, PermissionId = 1 }  // Member - book.read
            );
        }
    }
}
