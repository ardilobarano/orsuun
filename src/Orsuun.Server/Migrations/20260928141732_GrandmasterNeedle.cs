using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class GrandmasterNeedle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrandmasterNeedles",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrandmasterNeedles",
                table: "Accounts");
        }
    }
}
