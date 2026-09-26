using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Figures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Figure",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Characters made before figures keep the look they had: the Kestrel (1) and the Drumcaller (3) were drawn as women.
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""Figure"" = 1 WHERE ""Class"" IN (1, 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Figure",
                table: "Accounts");
        }
    }
}
