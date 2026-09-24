using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class HeroClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Class",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Class",
                table: "Accounts");
        }
    }
}
