using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing teams take the same defaults a new team gets (Mint, 50), so they satisfy the new foreign key and
            // check constraint before those are added below.
            migrationBuilder.AddColumn<int>(
                name: "AvatarColorId",
                schema: "Core",
                table: "Team",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmRecipientsAbove",
                schema: "Core",
                table: "Team",
                type: "int",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<string>(
                name: "DefaultSignature",
                schema: "Core",
                table: "Team",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamColor",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamColor", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "RolePermission",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 9, 2 });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "TeamColor",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Light mint", "Mint" },
                    { 2, "Seafoam", "Seafoam" },
                    { 3, "Mist", "Mist" },
                    { 4, "Sky", "Sky" },
                    { 5, "Andes green", "Green" },
                    { 6, "Ocean blue", "Ocean" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Team_AvatarColorId",
                schema: "Core",
                table: "Team",
                column: "AvatarColorId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Team_ConfirmRecipientsAbove",
                schema: "Core",
                table: "Team",
                sql: "[ConfirmRecipientsAbove] BETWEEN 1 AND 500");

            migrationBuilder.CreateIndex(
                name: "IX_TeamColor_Name",
                schema: "Core.Ref",
                table: "TeamColor",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Team_TeamColor_AvatarColorId",
                schema: "Core",
                table: "Team",
                column: "AvatarColorId",
                principalSchema: "Core.Ref",
                principalTable: "TeamColor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Team_TeamColor_AvatarColorId",
                schema: "Core",
                table: "Team");

            migrationBuilder.DropTable(
                name: "TeamColor",
                schema: "Core.Ref");

            migrationBuilder.DropIndex(
                name: "IX_Team_AvatarColorId",
                schema: "Core",
                table: "Team");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Team_ConfirmRecipientsAbove",
                schema: "Core",
                table: "Team");

            migrationBuilder.DeleteData(
                schema: "Core.Ref",
                table: "RolePermission",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 9, 2 });

            migrationBuilder.DropColumn(
                name: "AvatarColorId",
                schema: "Core",
                table: "Team");

            migrationBuilder.DropColumn(
                name: "ConfirmRecipientsAbove",
                schema: "Core",
                table: "Team");

            migrationBuilder.DropColumn(
                name: "DefaultSignature",
                schema: "Core",
                table: "Team");
        }
    }
}
