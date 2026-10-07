using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class LoadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Ledger_AccountId_Kind",
                table: "Ledger",
                columns: new[] { "AccountId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_LastHeartbeatUtc",
                table: "Accounts",
                column: "LastHeartbeatUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ledger_AccountId_Kind",
                table: "Ledger");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_LastHeartbeatUtc",
                table: "Accounts");
        }
    }
}
