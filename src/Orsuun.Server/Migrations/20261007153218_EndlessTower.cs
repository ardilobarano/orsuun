using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class EndlessTower : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TowerBest",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TowerBestUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TowerClimbDay",
                table: "Accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TowerClimbs",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TowerPaid",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TowerSeason",
                table: "Accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TowerTitle",
                table: "Accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TowerSeasons",
                columns: table => new
                {
                    Season = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SettledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Climbers = table.Column<int>(type: "integer", nullable: false),
                    Champions = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowerSeasons", x => x.Season);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TowerSeason_TowerBest",
                table: "Accounts",
                columns: new[] { "TowerSeason", "TowerBest" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TowerSeasons");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_TowerSeason_TowerBest",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TowerBest",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TowerBestUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TowerClimbDay",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TowerClimbs",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TowerPaid",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TowerSeason",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TowerTitle",
                table: "Accounts");
        }
    }
}
