using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Wardrobe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Amber",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "AmberPurchases",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Wardrobe",
                table: "Accounts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WornCompanion",
                table: "Accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WornMount",
                table: "Accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WornSkin",
                table: "Accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amber",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AmberPurchases",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Wardrobe",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "WornCompanion",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "WornMount",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "WornSkin",
                table: "Accounts");
        }
    }
}
