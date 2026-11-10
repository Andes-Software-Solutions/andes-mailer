using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Core.Ref");

            migrationBuilder.EnsureSchema(
                name: "Core");

            migrationBuilder.CreateTable(
                name: "Permission",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                schema: "Core.Ref",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermission_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "Core.Ref",
                        principalTable: "Permission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermission_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "Core.Ref",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                schema: "Core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRole_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "Core.Ref",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRole_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "Core",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Permission",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Adds a user from the directory", "CreateUser" },
                    { 2, "Changes a user's details", "ModifyUser" },
                    { 3, "Deactivates a user", "DeactivateUser" },
                    { 4, "Adds a user to a team", "CreateTeamUser" },
                    { 5, "Changes the details of a team's user", "ModifyTeamUser" },
                    { 6, "Deactivates a team's user", "DeactivateTeamUser" }
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Role",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Manages every user", "Administrator" },
                    { 2, "Manages the users of a team", "TeamAdministrator" }
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 4, 1 },
                    { 5, 1 },
                    { 6, 1 },
                    { 4, 2 },
                    { 5, 2 },
                    { 6, 2 }
                });

            // The first administrator, so someone can add the other users. Operators may change or deactivate this row, so it is
            // migration data rather than model HasData, which a later migration would revert. The id is the account's Entra object id.
            migrationBuilder.InsertData(
                schema: "Core",
                table: "User",
                columns: new[] { "Id", "FirstName", "LastName", "Email", "DateCreated" },
                values: new object[] { new Guid("4ee09cb0-d7e7-4be4-b82f-2674695eb9df"), "Rodrigo", "Rojas", "rorro.irg@gmail.com", new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero) });

            migrationBuilder.InsertData(
                schema: "Core",
                table: "UserRole",
                columns: new[] { "UserId", "RoleId" },
                values: new object[] { new Guid("4ee09cb0-d7e7-4be4-b82f-2674695eb9df"), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_Permission_Name",
                schema: "Core.Ref",
                table: "Permission",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Role_Name",
                schema: "Core.Ref",
                table: "Role",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionId",
                schema: "Core.Ref",
                table: "RolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_User_DateDeactivated",
                schema: "Core",
                table: "User",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                schema: "Core",
                table: "User",
                column: "Email",
                unique: true,
                filter: "[DateDeactivated] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleId",
                schema: "Core",
                table: "UserRole",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermission",
                schema: "Core.Ref");

            migrationBuilder.DropTable(
                name: "UserRole",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "Permission",
                schema: "Core.Ref");

            migrationBuilder.DropTable(
                name: "Role",
                schema: "Core.Ref");

            migrationBuilder.DropTable(
                name: "User",
                schema: "Core");
        }
    }
}
