using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ContentHash",
                schema: "Core",
                table: "EmailTemplateAttachment",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ContentHash",
                schema: "Core",
                table: "EmailAttachment",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "Core",
                table: "EmailTemplateAttachment");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "Core",
                table: "EmailAttachment");
        }
    }
}
