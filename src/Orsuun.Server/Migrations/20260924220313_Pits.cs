using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Pits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Laurels",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PitDay",
                table: "Accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PitFights",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PitLosses",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PitRating",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 1000);   // existing heroes start the ladder at Rules.Pits.StartRating

            migrationBuilder.AddColumn<int>(
                name: "PitRoll",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PitWins",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_PitRating",
                table: "Accounts",
                column: "PitRating");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_PitRating",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Laurels",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitDay",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitFights",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitLosses",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitRating",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitRoll",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitWins",
                table: "Accounts");
        }
    }
}
