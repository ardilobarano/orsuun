using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class FishMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MealFish",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "MealUntilUtc",
                table: "Accounts");

            migrationBuilder.AddColumn<string>(
                name: "Meals",
                table: "Accounts",
                type: "character varying(96)",
                maxLength: 96,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Meals",
                table: "Accounts");

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
        }
    }
}
