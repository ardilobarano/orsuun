using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <summary>
    /// Maps 11 and 12 (26 Sep 2026) take campaign stages 101-120, so the zones move from 101-121 to 201-221
    /// (Content.FirstZoneId): heroes parked in a zone keep their zone. Dungeon floors (301-399) are never parked.
    /// </summary>
    public partial class MoveZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Accounts" SET "ParkedStage" = "ParkedStage" + 100 WHERE "ParkedStage" BETWEEN 101 AND 199;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Accounts" SET "ParkedStage" = 100 WHERE "ParkedStage" BETWEEN 101 AND 120;""");
            migrationBuilder.Sql("""UPDATE "Accounts" SET "ParkedStage" = "ParkedStage" - 100 WHERE "ParkedStage" BETWEEN 201 AND 299;""");
        }
    }
}
