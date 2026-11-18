using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddDuplicateTeamEmailsPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Permission",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 14, "Duplicates a team's emails and email templates", "DuplicateTeamEmails" });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 14, 1 },
                    { 14, 2 },
                    { 14, 3 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 14, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 14, 2 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 14, 3 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 14);
        }
    }
}
