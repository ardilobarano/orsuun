using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class CommanderPushes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CommanderPushUtc",
                table: "Logins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NoCommanderPushes",
                table: "Logins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnnouncedUtc",
                table: "BossClocks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommanderPushUtc",
                table: "Logins");

            migrationBuilder.DropColumn(
                name: "NoCommanderPushes",
                table: "Logins");

            migrationBuilder.DropColumn(
                name: "AnnouncedUtc",
                table: "BossClocks");
        }
    }
}
