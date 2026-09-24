using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Guilds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FlagGuildId",
                table: "Fortresses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GuildDonated",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "GuildDonatedToday",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "GuildDonationDay",
                table: "Accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "GuildId",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GuildJoinedUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GuildRank",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Tallies",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NameKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tag = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Open = table.Column<bool>(type: "boolean", nullable: false),
                    Treasury = table.Column<long>(type: "bigint", nullable: false),
                    Xp = table.Column<long>(type: "bigint", nullable: false),
                    Plunder = table.Column<int>(type: "integer", nullable: false),
                    Muster = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEvent = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_GuildId",
                table: "Accounts",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_NameKey",
                table: "Guilds",
                column: "NameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_Tag",
                table: "Guilds",
                column: "Tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_Xp",
                table: "Guilds",
                column: "Xp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_GuildId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "FlagGuildId",
                table: "Fortresses");

            migrationBuilder.DropColumn(
                name: "GuildDonated",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "GuildDonatedToday",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "GuildDonationDay",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "GuildId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "GuildJoinedUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "GuildRank",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Tallies",
                table: "Accounts");
        }
    }
}
