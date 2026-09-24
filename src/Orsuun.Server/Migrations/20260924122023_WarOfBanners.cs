using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class WarOfBanners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "HpLeft",
                table: "BossClocks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "HpMax",
                table: "BossClocks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "PoolSpawnUtc",
                table: "BossClocks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "SlainBanner",
                table: "BossClocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SlainBy",
                table: "BossClocks",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlainUtc",
                table: "BossClocks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Banner",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Bounties",
                table: "Accounts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HuntMarks",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSiegeUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PinningWax",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SwornUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BannerScores",
                columns: table => new
                {
                    Season = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Banner = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannerScores", x => new { x.Season, x.Banner });
                });

            migrationBuilder.CreateTable(
                name: "BossHits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BossId = table.Column<int>(type: "integer", nullable: false),
                    SpawnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Banner = table.Column<int>(type: "integer", nullable: false),
                    Damage = table.Column<long>(type: "bigint", nullable: false),
                    Utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BossHits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fortresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Holder = table.Column<int>(type: "integer", nullable: false),
                    HeldSinceUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    WallMax = table.Column<long>(type: "bigint", nullable: false),
                    Wall = table.Column<long>(type: "bigint", nullable: false),
                    SiegeEmber = table.Column<long>(type: "bigint", nullable: false),
                    SiegeSky = table.Column<long>(type: "bigint", nullable: false),
                    SiegeGold = table.Column<long>(type: "bigint", nullable: false),
                    LastEvent = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fortresses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BossHits_BossId_SpawnUtc",
                table: "BossHits",
                columns: new[] { "BossId", "SpawnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BannerScores");

            migrationBuilder.DropTable(
                name: "BossHits");

            migrationBuilder.DropTable(
                name: "Fortresses");

            migrationBuilder.DropColumn(
                name: "HpLeft",
                table: "BossClocks");

            migrationBuilder.DropColumn(
                name: "HpMax",
                table: "BossClocks");

            migrationBuilder.DropColumn(
                name: "PoolSpawnUtc",
                table: "BossClocks");

            migrationBuilder.DropColumn(
                name: "SlainBanner",
                table: "BossClocks");

            migrationBuilder.DropColumn(
                name: "SlainBy",
                table: "BossClocks");

            migrationBuilder.DropColumn(
                name: "SlainUtc",
                table: "BossClocks");

            migrationBuilder.DropColumn(
                name: "Banner",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Bounties",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "HuntMarks",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "LastSiegeUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PinningWax",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SwornUtc",
                table: "Accounts");
        }
    }
}
