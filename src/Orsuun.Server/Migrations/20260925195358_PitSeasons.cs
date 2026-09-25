using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class PitSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PitLastLaurels",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PitLastRank",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PitLastRating",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PitLastSeason",
                table: "Accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PitSeason",
                table: "Accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PitSeasonLosses",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PitSeasonWins",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PitTitle",
                table: "Accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PitSeasons",
                columns: table => new
                {
                    Season = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SettledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Fighters = table.Column<int>(type: "integer", nullable: false),
                    Champions = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PitSeasons", x => x.Season);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PitSeasons");

            migrationBuilder.DropColumn(
                name: "PitLastLaurels",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitLastRank",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitLastRating",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitLastSeason",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitSeason",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitSeasonLosses",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitSeasonWins",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PitTitle",
                table: "Accounts");
        }
    }
}
