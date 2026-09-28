using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Fishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AtRiver",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoFromUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoRodUntilUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CastBiteMs",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CastUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fish",
                table: "Accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MealFish",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "MealUntilUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Mussels",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Pearls",
                table: "Accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtRiver",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AutoFromUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AutoRodUntilUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CastBiteMs",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CastUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Fish",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "MealFish",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "MealUntilUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Mussels",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Pearls",
                table: "Accounts");
        }
    }
}
