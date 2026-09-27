using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class GuildRaids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildRaidHits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RaidId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(56)", maxLength: 56, nullable: false),
                    Damage = table.Column<long>(type: "bigint", nullable: false),
                    Day = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildRaidHits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuildRaids",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Week = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Map = table.Column<int>(type: "integer", nullable: false),
                    Members = table.Column<int>(type: "integer", nullable: false),
                    HpMax = table.Column<long>(type: "bigint", nullable: false),
                    HpLeft = table.Column<long>(type: "bigint", nullable: false),
                    StartsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SlainUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SlainBy = table.Column<string>(type: "character varying(56)", maxLength: 56, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildRaids", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildRaidHits_RaidId_AccountId_Day",
                table: "GuildRaidHits",
                columns: new[] { "RaidId", "AccountId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildRaids_GuildId_Week",
                table: "GuildRaids",
                columns: new[] { "GuildId", "Week" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildRaidHits");

            migrationBuilder.DropTable(
                name: "GuildRaids");
        }
    }
}
