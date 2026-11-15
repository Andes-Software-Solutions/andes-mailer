using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailsAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttachmentStatus",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentType",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailStatus",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecipientType",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipientType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Email",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Email", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Email_EmailStatus_StatusId",
                        column: x => x.StatusId,
                        principalSchema: "Core.Ref",
                        principalTable: "EmailStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Email_User_SenderId",
                        column: x => x.SenderId,
                        principalSchema: "Core",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailAttachment",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_EmailAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailAttachment_AttachmentStatus_StatusId",
                        column: x => x.StatusId,
                        principalSchema: "Core.Ref",
                        principalTable: "AttachmentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailAttachment_DocumentType_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "Core.Ref",
                        principalTable: "DocumentType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailAttachment_Email_EmailId",
                        column: x => x.EmailId,
                        principalSchema: "Core",
                        principalTable: "Email",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailRecipient",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientTypeId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRecipient", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailRecipient_Email_EmailId",
                        column: x => x.EmailId,
                        principalSchema: "Core",
                        principalTable: "Email",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailRecipient_RecipientType_RecipientTypeId",
                        column: x => x.RecipientTypeId,
                        principalSchema: "Core.Ref",
                        principalTable: "RecipientType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "AttachmentStatus",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Received and waiting to be stored", "Pending" },
                    { 2, "Being written to storage", "Uploading" },
                    { 3, "Stored and ready to download", "Uploaded" },
                    { 4, "Could not be stored", "Failed" }
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "DocumentType",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "PDF document (.pdf)", "Pdf" },
                    { 2, "Word document (.docx)", "Docx" },
                    { 3, "PowerPoint presentation (.pptx)", "Pptx" },
                    { 4, "PNG image (.png)", "Png" },
                    { 5, "JPEG image (.jpg, .jpeg)", "Jpeg" },
                    { 6, "GIF image (.gif)", "Gif" },
                    { 7, "WebP image (.webp)", "Webp" }
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "EmailStatus",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Being composed", "Draft" },
                    { 2, "Sent; reserved until sending is built", "Sent" }
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Permission",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 7, "Uploads documents to, and removes them from, an email", "CanAttachDocuments" });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "RecipientType",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Primary recipient", "To" },
                    { 2, "Copied recipient", "Cc" },
                    { 3, "Blind-copied recipient", "Bcc" }
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 7, 1 },
                    { 7, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentStatus_Name",
                schema: "Core.Ref",
                table: "AttachmentStatus",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentType_Name",
                schema: "Core.Ref",
                table: "DocumentType",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Email_DateDeactivated",
                schema: "Core",
                table: "Email",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_Email_SenderId_StatusId",
                schema: "Core",
                table: "Email",
                columns: new[] { "SenderId", "StatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Email_StatusId",
                schema: "Core",
                table: "Email",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachment_DateDeactivated",
                schema: "Core",
                table: "EmailAttachment",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachment_DocumentTypeId",
                schema: "Core",
                table: "EmailAttachment",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachment_EmailId",
                schema: "Core",
                table: "EmailAttachment",
                column: "EmailId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachment_StatusId",
                schema: "Core",
                table: "EmailAttachment",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipient_EmailId_Address",
                schema: "Core",
                table: "EmailRecipient",
                columns: new[] { "EmailId", "Address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipient_RecipientTypeId",
                schema: "Core",
                table: "EmailRecipient",
                column: "RecipientTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailStatus_Name",
                schema: "Core.Ref",
                table: "EmailStatus",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipientType_Name",
                schema: "Core.Ref",
                table: "RecipientType",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailAttachment",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "EmailRecipient",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "AttachmentStatus",
                schema: "Core.Ref");

            migrationBuilder.DropTable(
                name: "DocumentType",
                schema: "Core.Ref");

            migrationBuilder.DropTable(
                name: "Email",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "RecipientType",
                schema: "Core.Ref");

            migrationBuilder.DropTable(
                name: "EmailStatus",
                schema: "Core.Ref");

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 7, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 7, 2 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 7);
        }
    }
}
