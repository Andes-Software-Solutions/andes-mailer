using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Irreversible: emails now belong to a team, and the drafts made before teams existed have none to move to.
            // Their documents stay in the attachments container; clear it by hand (in Azurite locally) if they matter.
            migrationBuilder.Sql("DELETE FROM [Core].[EmailAttachment];");
            migrationBuilder.Sql("DELETE FROM [Core].[EmailRecipient];");
            migrationBuilder.Sql("DELETE FROM [Core].[Email];");

            // Team Administrator is held per team from now on, through [Core].[TeamMember]; a global grant of it means nothing
            // and would fail CK_UserRole_RoleId below.
            migrationBuilder.Sql("DELETE FROM [Core].[UserRole] WHERE [RoleId] = 2;");

            migrationBuilder.DropForeignKey(
                name: "FK_Email_User_SenderId",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropIndex(
                name: "IX_Email_SenderId_StatusId",
                schema: "Core",
                table: "Email");

            migrationBuilder.RenameColumn(
                name: "SenderId",
                schema: "Core",
                table: "Email",
                newName: "AuthorId");

            // The table is empty after the deletes above, so the placeholder default never lands on a row.
            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                schema: "Core",
                table: "Email",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Team",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ServiceAccountEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ServiceAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Team", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamMember",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMember", x => x.Id);
                    table.CheckConstraint("CK_TeamMember_RoleId", "[RoleId] IN (2, 3)");
                    table.ForeignKey(
                        name: "FK_TeamMember_Role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "Core.Ref",
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMember_Team_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Core",
                        principalTable: "Team",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMember_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "Core",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Permission",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 8, "Creates a team", "CreateTeam" },
                    { 9, "Changes a team's details", "ModifyTeam" },
                    { 10, "Deactivates a team", "DeactivateTeam" },
                    { 11, "Reads a team and its members", "ViewTeam" },
                    { 12, "Reads, creates, changes, and deletes a team's emails", "ComposeTeamEmails" }
                });

            migrationBuilder.UpdateData(
                schema: "Core.Ref",
                table: "Role",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "Manages every user and team, and can work in any team");

            migrationBuilder.UpdateData(
                schema: "Core.Ref",
                table: "Role",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Manages the members of a team and composes its emails");

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Role",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 3, "Composes the emails of a team", "TeamMember" });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 8, 1 },
                    { 9, 1 },
                    { 10, 1 },
                    { 11, 1 },
                    { 12, 1 },
                    { 11, 2 },
                    { 12, 2 },
                    { 7, 3 },
                    { 11, 3 },
                    { 12, 3 }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserRole_RoleId",
                schema: "Core",
                table: "UserRole",
                sql: "[RoleId] IN (1)");

            migrationBuilder.CreateIndex(
                name: "IX_Email_AuthorId",
                schema: "Core",
                table: "Email",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Email_TeamId_StatusId",
                schema: "Core",
                table: "Email",
                columns: new[] { "TeamId", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Team_DateDeactivated",
                schema: "Core",
                table: "Team",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_Team_Name",
                schema: "Core",
                table: "Team",
                column: "Name",
                unique: true,
                filter: "[DateDeactivated] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Team_ServiceAccountEmail",
                schema: "Core",
                table: "Team",
                column: "ServiceAccountEmail",
                unique: true,
                filter: "[DateDeactivated] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMember_DateDeactivated",
                schema: "Core",
                table: "TeamMember",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMember_RoleId",
                schema: "Core",
                table: "TeamMember",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMember_TeamId_UserId",
                schema: "Core",
                table: "TeamMember",
                columns: new[] { "TeamId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMember_UserId",
                schema: "Core",
                table: "TeamMember",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Email_Team_TeamId",
                schema: "Core",
                table: "Email",
                column: "TeamId",
                principalSchema: "Core",
                principalTable: "Team",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Email_User_AuthorId",
                schema: "Core",
                table: "Email",
                column: "AuthorId",
                principalSchema: "Core",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema only: the emails and global Team Administrator grants Up deleted cannot come back. Emails made since
            // belong to a team, which no longer exists after this, so they go too.
            migrationBuilder.Sql("DELETE FROM [Core].[EmailAttachment];");
            migrationBuilder.Sql("DELETE FROM [Core].[EmailRecipient];");
            migrationBuilder.Sql("DELETE FROM [Core].[Email];");

            migrationBuilder.DropForeignKey(
                name: "FK_Email_Team_TeamId",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropForeignKey(
                name: "FK_Email_User_AuthorId",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropTable(
                name: "TeamMember",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "Team",
                schema: "Core");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserRole_RoleId",
                schema: "Core",
                table: "UserRole");

            migrationBuilder.DropIndex(
                name: "IX_Email_AuthorId",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropIndex(
                name: "IX_Email_TeamId_StatusId",
                schema: "Core",
                table: "Email");

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 8, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 9, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 10, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 11, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 12, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 11, 2 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 12, 2 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 7, 3 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 11, 3 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 12, 3 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Role",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "TeamId",
                schema: "Core",
                table: "Email");

            migrationBuilder.RenameColumn(
                name: "AuthorId",
                schema: "Core",
                table: "Email",
                newName: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Email_SenderId_StatusId",
                schema: "Core",
                table: "Email",
                columns: new[] { "SenderId", "StatusId" });

            migrationBuilder.UpdateData(
                schema: "Core.Ref",
                table: "Role",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "Manages every user");

            migrationBuilder.UpdateData(
                schema: "Core.Ref",
                table: "Role",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Manages the users of a team");

            migrationBuilder.AddForeignKey(
                name: "FK_Email_User_SenderId",
                schema: "Core",
                table: "Email",
                column: "SenderId",
                principalSchema: "Core",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
