using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Recovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Logins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResetHash",
                table: "Logins",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResetTries",
                table: "Logins",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetUntilUtc",
                table: "Logins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifyHash",
                table: "Logins",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifyUntilUtc",
                table: "Logins",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "Logins");

            migrationBuilder.DropColumn(
                name: "ResetHash",
                table: "Logins");

            migrationBuilder.DropColumn(
                name: "ResetTries",
                table: "Logins");

            migrationBuilder.DropColumn(
                name: "ResetUntilUtc",
                table: "Logins");

            migrationBuilder.DropColumn(
                name: "VerifyHash",
                table: "Logins");

            migrationBuilder.DropColumn(
                name: "VerifyUntilUtc",
                table: "Logins");
        }
    }
}
