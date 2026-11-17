using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                schema: "Core",
                table: "Email",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailTemplate",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplate_Team_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Core",
                        principalTable: "Team",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailTemplate_User_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "Core",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplateAttachment",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(127)", maxLength: 127, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplateAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplateAttachment_AttachmentStatus_StatusId",
                        column: x => x.StatusId,
                        principalSchema: "Core.Ref",
                        principalTable: "AttachmentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailTemplateAttachment_DocumentType_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "Core.Ref",
                        principalTable: "DocumentType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailTemplateAttachment_EmailTemplate_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "Core",
                        principalTable: "EmailTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplateRecipient",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientTypeId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplateRecipient", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplateRecipient_EmailTemplate_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "Core",
                        principalTable: "EmailTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailTemplateRecipient_RecipientType_RecipientTypeId",
                        column: x => x.RecipientTypeId,
                        principalSchema: "Core.Ref",
                        principalTable: "RecipientType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Permission",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 13, "Creates, changes, and deactivates a team's email templates", "ManageTeamTemplates" });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 13, 1 },
                    { 13, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Email_TemplateId",
                schema: "Core",
                table: "Email",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplate_AuthorId",
                schema: "Core",
                table: "EmailTemplate",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplate_DateDeactivated",
                schema: "Core",
                table: "EmailTemplate",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplate_TeamId_Name",
                schema: "Core",
                table: "EmailTemplate",
                columns: new[] { "TeamId", "Name" },
                unique: true,
                filter: "[DateDeactivated] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateAttachment_DateDeactivated",
                schema: "Core",
                table: "EmailTemplateAttachment",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateAttachment_DocumentTypeId",
                schema: "Core",
                table: "EmailTemplateAttachment",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateAttachment_StatusId",
                schema: "Core",
                table: "EmailTemplateAttachment",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateAttachment_TemplateId",
                schema: "Core",
                table: "EmailTemplateAttachment",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateRecipient_RecipientTypeId",
                schema: "Core",
                table: "EmailTemplateRecipient",
                column: "RecipientTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplateRecipient_TemplateId_Address",
                schema: "Core",
                table: "EmailTemplateRecipient",
                columns: new[] { "TemplateId", "Address" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Email_EmailTemplate_TemplateId",
                schema: "Core",
                table: "Email",
                column: "TemplateId",
                principalSchema: "Core",
                principalTable: "EmailTemplate",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Email_EmailTemplate_TemplateId",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropTable(
                name: "EmailTemplateAttachment",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "EmailTemplateRecipient",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "EmailTemplate",
                schema: "Core");

            migrationBuilder.DropIndex(
                name: "IX_Email_TemplateId",
                schema: "Core",
                table: "Email");

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 13, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 13, 2 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DropColumn(
                name: "TemplateId",
                schema: "Core",
                table: "Email");
        }
    }
}
