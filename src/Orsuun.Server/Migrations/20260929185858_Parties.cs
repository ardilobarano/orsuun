using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Parties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PartyInviteFrom",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartyInviteName",
                table: "Accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PartyInviteUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PartyJoinedUtc",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PartyLeaderId",
                table: "Accounts",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PartyInviteFrom",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PartyInviteName",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PartyInviteUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PartyJoinedUtc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PartyLeaderId",
                table: "Accounts");
        }
    }
}
