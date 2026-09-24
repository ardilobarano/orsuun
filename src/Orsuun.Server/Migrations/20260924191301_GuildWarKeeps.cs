using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class GuildWarKeeps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarDraws",
                table: "Guilds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WarLosses",
                table: "Guilds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Guilds founded before the war start at the ladder's starting rating (Rules.GuildWars.StartRating).
            migrationBuilder.AddColumn<int>(
                name: "WarRating",
                table: "Guilds",
                type: "integer",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "WarWins",
                table: "Guilds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "KeepEndsUtc",
                table: "Fortresses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "KeepMended",
                table: "Fortresses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "KeepStartsUtc",
                table: "Fortresses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "KeepState",
                table: "Fortresses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "KeepWeek",
                table: "Fortresses",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "FortressBids",
                columns: table => new
                {
                    Week = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GuildId = table.Column<Guid>(type: "uuid", nullable: false),
                    FortressId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Contender = table.Column<bool>(type: "boolean", nullable: false),
                    Refunded = table.Column<bool>(type: "boolean", nullable: false),
                    Damage = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FortressBids", x => new { x.Week, x.GuildId });
                });

            migrationBuilder.CreateTable(
                name: "GuildWarEntries",
                columns: table => new
                {
                    WarId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fights = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    LastUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildWarEntries", x => new { x.WarId, x.AccountId });
                });

            migrationBuilder.CreateTable(
                name: "GuildWarNights",
                columns: table => new
                {
                    Night = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    StartsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Paired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildWarNights", x => x.Night);
                });

            migrationBuilder.CreateTable(
                name: "GuildWars",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Night = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    GuildA = table.Column<Guid>(type: "uuid", nullable: false),
                    GuildB = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KillsA = table.Column<int>(type: "integer", nullable: false),
                    KillsB = table.Column<int>(type: "integer", nullable: false),
                    Front0 = table.Column<int>(type: "integer", nullable: false),
                    Front1 = table.Column<int>(type: "integer", nullable: false),
                    Front2 = table.Column<int>(type: "integer", nullable: false),
                    FlagA = table.Column<int>(type: "integer", nullable: false),
                    FlagB = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    LastEvent = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildWars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuildWarSignups",
                columns: table => new
                {
                    Night = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    GuildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildWarSignups", x => new { x.Night, x.GuildId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_FortressBids_Week_FortressId",
                table: "FortressBids",
                columns: new[] { "Week", "FortressId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildWarEntries_AccountId",
                table: "GuildWarEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildWars_Night_GuildA",
                table: "GuildWars",
                columns: new[] { "Night", "GuildA" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildWars_Night_GuildB",
                table: "GuildWars",
                columns: new[] { "Night", "GuildB" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildWars_Result_EndsUtc",
                table: "GuildWars",
                columns: new[] { "Result", "EndsUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FortressBids");

            migrationBuilder.DropTable(
                name: "GuildWarEntries");

            migrationBuilder.DropTable(
                name: "GuildWarNights");

            migrationBuilder.DropTable(
                name: "GuildWars");

            migrationBuilder.DropTable(
                name: "GuildWarSignups");

            migrationBuilder.DropColumn(
                name: "WarDraws",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "WarLosses",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "WarRating",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "WarWins",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "KeepEndsUtc",
                table: "Fortresses");

            migrationBuilder.DropColumn(
                name: "KeepMended",
                table: "Fortresses");

            migrationBuilder.DropColumn(
                name: "KeepStartsUtc",
                table: "Fortresses");

            migrationBuilder.DropColumn(
                name: "KeepState",
                table: "Fortresses");

            migrationBuilder.DropColumn(
                name: "KeepWeek",
                table: "Fortresses");
        }
    }
}
