using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class MoreDungeons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DungeonPausedId",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MastersNeedles",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Runs already waiting before this migration are the Hollow Spire's (the only dungeon then).
            migrationBuilder.Sql("UPDATE \"Accounts\" SET \"DungeonPausedId\" = 1 WHERE \"DungeonRunAtSmith\" <> 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DungeonPausedId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "MastersNeedles",
                table: "Accounts");
        }
    }
}
