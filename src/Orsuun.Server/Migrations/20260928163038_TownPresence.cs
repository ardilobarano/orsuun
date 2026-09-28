using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class TownPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InTownUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InTownUtc",
                table: "Accounts");
        }
    }
}
