using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Invites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Accounts",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "InviteRewarded",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedById",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_InviteCode",
                table: "Accounts",
                column: "InviteCode",
                unique: true,
                filter: "\"InviteCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_InvitedById",
                table: "Accounts",
                column: "InvitedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_InviteCode",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_InvitedById",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "InviteRewarded",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "InvitedById",
                table: "Accounts");
        }
    }
}
