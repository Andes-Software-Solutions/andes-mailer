using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andes.Mailer.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamServiceAccountIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Team_ServiceAccountId",
                schema: "Core",
                table: "Team",
                column: "ServiceAccountId",
                unique: true,
                filter: "[DateDeactivated] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Team_ServiceAccountId",
                schema: "Core",
                table: "Team");
        }
    }
}
