using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddContactGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactGroup",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeactivatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactGroup_Team_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Core",
                        principalTable: "Team",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContactGroup_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "Core",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContactGroup_User_DeactivatedById",
                        column: x => x.DeactivatedById,
                        principalSchema: "Core",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactGroupMember",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    AddedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactGroupMember", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactGroupMember_ContactGroup_ContactGroupId",
                        column: x => x.ContactGroupId,
                        principalSchema: "Core",
                        principalTable: "ContactGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContactGroupMember_User_AddedById",
                        column: x => x.AddedById,
                        principalSchema: "Core",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContactGroupMember_User_RemovedById",
                        column: x => x.RemovedById,
                        principalSchema: "Core",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Permission",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 15, "Creates, changes, and deactivates a team's contact groups", "ManageTeamContactGroups" });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 15, 1 },
                    { 15, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactGroup_CreatedById",
                schema: "Core",
                table: "ContactGroup",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ContactGroup_DateDeactivated",
                schema: "Core",
                table: "ContactGroup",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_ContactGroup_DeactivatedById",
                schema: "Core",
                table: "ContactGroup",
                column: "DeactivatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ContactGroup_TeamId_Name",
                schema: "Core",
                table: "ContactGroup",
                columns: new[] { "TeamId", "Name" },
                unique: true,
                filter: "[DateDeactivated] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactGroupMember_AddedById",
                schema: "Core",
                table: "ContactGroupMember",
                column: "AddedById");

            migrationBuilder.CreateIndex(
                name: "IX_ContactGroupMember_ContactGroupId_Address",
                schema: "Core",
                table: "ContactGroupMember",
                columns: new[] { "ContactGroupId", "Address" },
                unique: true,
                filter: "[DateDeactivated] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactGroupMember_DateDeactivated",
                schema: "Core",
                table: "ContactGroupMember",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_ContactGroupMember_RemovedById",
                schema: "Core",
                table: "ContactGroupMember",
                column: "RemovedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactGroupMember",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "ContactGroup",
                schema: "Core");

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 15, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 15, 2 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 15);
        }
    }
}
