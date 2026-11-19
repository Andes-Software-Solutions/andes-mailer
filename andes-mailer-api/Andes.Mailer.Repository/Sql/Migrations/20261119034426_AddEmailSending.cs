using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Email_StatusId",
                schema: "Core",
                table: "Email");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DateCompleted",
                schema: "Core",
                table: "Email",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DateSendRequested",
                schema: "Core",
                table: "Email",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledFor",
                schema: "Core",
                table: "Email",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SentById",
                schema: "Core",
                table: "Email",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeliveryStatus",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailDelivery",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GraphMessageId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DraftMailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DateSent = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDelivery", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailDelivery_DeliveryStatus_StatusId",
                        column: x => x.StatusId,
                        principalSchema: "Core.Ref",
                        principalTable: "DeliveryStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailDelivery_EmailRecipient_RecipientId",
                        column: x => x.RecipientId,
                        principalSchema: "Core",
                        principalTable: "EmailRecipient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailDelivery_Email_EmailId",
                        column: x => x.EmailId,
                        principalSchema: "Core",
                        principalTable: "Email",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "DeliveryStatus",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Waiting for its next attempt", "Pending" },
                    { 2, "Being sent", "Sending" },
                    { 3, "Accepted by the mail server", "Sent" },
                    { 4, "Every attempt failed", "Failed" }
                });

            migrationBuilder.UpdateData(
                schema: "Core.Ref",
                table: "EmailStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Sent to every recipient");

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "EmailStatus",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 3, "Waiting for its scheduled time", "Scheduled" },
                    { 4, "Being delivered to its recipients", "Sending" },
                    { 5, "Sent to some recipients; the rest failed", "PartiallySent" },
                    { 6, "No recipient received it", "Failed" }
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Permission",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 16, "Sends or schedules a team's emails", "SendTeamEmails" });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 16, 1 },
                    { 16, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Email_SentById",
                schema: "Core",
                table: "Email",
                column: "SentById");

            migrationBuilder.CreateIndex(
                name: "IX_Email_StatusId_ScheduledFor",
                schema: "Core",
                table: "Email",
                columns: new[] { "StatusId", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryStatus_Name",
                schema: "Core.Ref",
                table: "DeliveryStatus",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailDelivery_DateDeactivated",
                schema: "Core",
                table: "EmailDelivery",
                column: "DateDeactivated");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDelivery_EmailId_RecipientId",
                schema: "Core",
                table: "EmailDelivery",
                columns: new[] { "EmailId", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailDelivery_RecipientId",
                schema: "Core",
                table: "EmailDelivery",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDelivery_StatusId_NextAttemptAt",
                schema: "Core",
                table: "EmailDelivery",
                columns: new[] { "StatusId", "NextAttemptAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Email_User_SentById",
                schema: "Core",
                table: "Email",
                column: "SentById",
                principalSchema: "Core",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Email_User_SentById",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropTable(
                name: "EmailDelivery",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "DeliveryStatus",
                schema: "Core.Ref");

            migrationBuilder.DropIndex(
                name: "IX_Email_SentById",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropIndex(
                name: "IX_Email_StatusId_ScheduledFor",
                schema: "Core",
                table: "Email");

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "EmailStatus",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "EmailStatus",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "EmailStatus",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "EmailStatus",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 16, 1 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 16, 2 });

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "Permission",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DropColumn(
                name: "DateCompleted",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropColumn(
                name: "DateSendRequested",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropColumn(
                name: "ScheduledFor",
                schema: "Core",
                table: "Email");

            migrationBuilder.DropColumn(
                name: "SentById",
                schema: "Core",
                table: "Email");

            migrationBuilder.UpdateData(
                schema: "Core.Ref",
                table: "EmailStatus",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Sent; reserved until sending is built");

            migrationBuilder.CreateIndex(
                name: "IX_Email_StatusId",
                schema: "Core",
                table: "Email",
                column: "StatusId");
        }
    }
}
