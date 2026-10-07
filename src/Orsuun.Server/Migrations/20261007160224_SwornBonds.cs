using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class SwornBonds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BondAskFrom",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BondAskName",
                table: "Accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BondAskUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BondEndedUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BondPartnerId",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BondPartnerName",
                table: "Accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "BondSeconds",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "BondSinceUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BondAskFrom",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BondAskName",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BondAskUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BondEndedUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BondPartnerId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BondPartnerName",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BondSeconds",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BondSinceUtc",
                table: "Accounts");
        }
    }
}
