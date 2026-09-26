using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <summary>
    /// Average damage and skill damage on weapons of item level 30 and up (owner, 26 Sep 2026; Rules.WeaponRolls). New
    /// drops roll them in the rules; weapons that already exist roll once here, from a bell curve near the rules' own
    /// (average damage around +8% within -30..60, skill damage around +4% within -15..30).
    /// </summary>
    public partial class WeaponDamageRolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AverageDamage",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkillDamage",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "Items" SET
                    "AverageDamage" = LEAST(60, GREATEST(-30, round(random_normal(8, 14))::int)),
                    "SkillDamage" = LEAST(30, GREATEST(-15, round(random_normal(4, 7))::int))
                WHERE "Slot" = 0 AND "ItemLevel" >= 30;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageDamage",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SkillDamage",
                table: "Items");
        }
    }
}
