using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Moderation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Reviewed",
                table: "ChatMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BanReason",
                table: "Accounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BannedUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MutedUntilUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminActions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Admin = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Target = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_AccountId",
                table: "ChatMessages",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_Reports_Reviewed",
                table: "ChatMessages",
                columns: new[] { "Reports", "Reviewed" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminActions_Utc",
                table: "AdminActions",
                column: "Utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminActions");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_AccountId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_Reports_Reviewed",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "Reviewed",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "BanReason",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BannedUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "MutedUntilUtc",
                table: "Accounts");
        }
    }
}
