using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Books",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ISBN",
                table: "Books",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Author",
                table: "Books",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // امنع التكرار: اربط الدور 1 (مثلاً Admin) بكل الصلاحيات 1..12
            // ربط كل الصلاحيات (1..12) بالدور 1 إن كان موجودًا، بدون تكرار
            migrationBuilder.Sql(@"
DECLARE @rid INT = 1; -- RoleId = 1 (Admin?)
IF EXISTS (SELECT 1 FROM [Roles] WHERE [Id] = @rid)
BEGIN
    INSERT INTO [RolePermissions]([RoleId],[PermissionId])
    SELECT @rid, p.Id
    FROM [Permissions] p
    WHERE p.Id BETWEEN 1 AND 12
      AND NOT EXISTS (
          SELECT 1 FROM [RolePermissions] rp
          WHERE rp.RoleId = @rid AND rp.PermissionId = p.Id
      );
END
");
            migrationBuilder.Sql(@"
DECLARE @rid2 INT = 2; -- RoleId = 2 (User?)
IF EXISTS (SELECT 1 FROM [Roles] WHERE [Id] = @rid2)
BEGIN
    IF EXISTS (SELECT 1 FROM [Permissions] WHERE [Id]=1)
    AND NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [RoleId]=@rid2 AND [PermissionId]=1)
        INSERT INTO [RolePermissions]([RoleId],[PermissionId]) VALUES (@rid2, 1);

    IF EXISTS (SELECT 1 FROM [Permissions] WHERE [Id]=8)
    AND NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [RoleId]=@rid2 AND [PermissionId]=8)
        INSERT INTO [RolePermissions]([RoleId],[PermissionId]) VALUES (@rid2, 8);

    IF EXISTS (SELECT 1 FROM [Permissions] WHERE [Id]=9)
    AND NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [RoleId]=@rid2 AND [PermissionId]=9)
        INSERT INTO [RolePermissions]([RoleId],[PermissionId]) VALUES (@rid2, 9);
END
");



            migrationBuilder.CreateIndex(
                name: "IX_Books_Author",
                table: "Books",
                column: "Author");

            migrationBuilder.CreateIndex(
                name: "IX_Books_ISBN",
                table: "Books",
                column: "ISBN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Books_Title",
                table: "Books",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_Author",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_ISBN",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_Books_Title",
                table: "Books");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 8, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 9, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 10, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 11, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 12, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 8, 2 });

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Books",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ISBN",
                table: "Books",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Author",
                table: "Books",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);
        }
    }
}
