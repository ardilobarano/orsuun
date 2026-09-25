using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class SkillBooksHonor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FromBooks",
                table: "Trades",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ToBooks",
                table: "Trades",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BookCount",
                table: "MarketListings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BookId",
                table: "MarketListings",
                type: "integer",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.AddColumn<long>(
                name: "Honor",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "SkillGrades",
                table: "Accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SkillProgress",
                table: "Accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SkillReads",
                table: "Accounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BookStacks",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookId = table.Column<int>(type: "integer", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookStacks", x => new { x.AccountId, x.BookId });
                    table.ForeignKey(
                        name: "FK_BookStacks_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookStacks");

            migrationBuilder.DropColumn(
                name: "FromBooks",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "ToBooks",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "BookCount",
                table: "MarketListings");

            migrationBuilder.DropColumn(
                name: "BookId",
                table: "MarketListings");

            migrationBuilder.DropColumn(
                name: "Honor",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SkillGrades",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SkillProgress",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "SkillReads",
                table: "Accounts");
        }
    }
}
