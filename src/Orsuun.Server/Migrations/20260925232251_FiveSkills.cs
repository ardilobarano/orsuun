using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class FiveSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SkillReads",
                table: "Accounts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            // Five skills a class (owner, 26 Sep 2026): book ids move from class * 3 + slot to class * 5 + slot, in the
            // stored grades, reads and rests (twelve values become twenty), the scroll stacks (through +1000 so the
            // primary key never collides), scroll listings and scrolls on open trade tables.
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""SkillGrades"" = concat_ws(';', split_part(""SkillGrades"", ';', 1), split_part(""SkillGrades"", ';', 2), split_part(""SkillGrades"", ';', 3), '', '', split_part(""SkillGrades"", ';', 4), split_part(""SkillGrades"", ';', 5), split_part(""SkillGrades"", ';', 6), '', '', split_part(""SkillGrades"", ';', 7), split_part(""SkillGrades"", ';', 8), split_part(""SkillGrades"", ';', 9), '', '', split_part(""SkillGrades"", ';', 10), split_part(""SkillGrades"", ';', 11), split_part(""SkillGrades"", ';', 12), '', '') WHERE ""SkillGrades"" <> '';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""SkillProgress"" = concat_ws(';', split_part(""SkillProgress"", ';', 1), split_part(""SkillProgress"", ';', 2), split_part(""SkillProgress"", ';', 3), '', '', split_part(""SkillProgress"", ';', 4), split_part(""SkillProgress"", ';', 5), split_part(""SkillProgress"", ';', 6), '', '', split_part(""SkillProgress"", ';', 7), split_part(""SkillProgress"", ';', 8), split_part(""SkillProgress"", ';', 9), '', '', split_part(""SkillProgress"", ';', 10), split_part(""SkillProgress"", ';', 11), split_part(""SkillProgress"", ';', 12), '', '') WHERE ""SkillProgress"" <> '';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""SkillReads"" = concat_ws(';', split_part(""SkillReads"", ';', 1), split_part(""SkillReads"", ';', 2), split_part(""SkillReads"", ';', 3), '', '', split_part(""SkillReads"", ';', 4), split_part(""SkillReads"", ';', 5), split_part(""SkillReads"", ';', 6), '', '', split_part(""SkillReads"", ';', 7), split_part(""SkillReads"", ';', 8), split_part(""SkillReads"", ';', 9), '', '', split_part(""SkillReads"", ';', 10), split_part(""SkillReads"", ';', 11), split_part(""SkillReads"", ';', 12), '', '') WHERE ""SkillReads"" <> '';");
            migrationBuilder.Sql(@"UPDATE ""BookStacks"" SET ""BookId"" = ""BookId"" + 1000;");
            migrationBuilder.Sql(@"UPDATE ""BookStacks"" SET ""BookId"" = ((""BookId"" - 1000) / 3) * 5 + (""BookId"" - 1000) % 3;");
            migrationBuilder.Sql(@"UPDATE ""MarketListings"" SET ""BookId"" = (""BookId"" / 3) * 5 + ""BookId"" % 3 WHERE ""BookId"" >= 0;");
            migrationBuilder.Sql(@"UPDATE ""Trades"" SET ""FromBooks"" = COALESCE((SELECT string_agg(((split_part(p, ':', 1)::int / 3) * 5 + split_part(p, ':', 1)::int % 3)::text || ':' || split_part(p, ':', 2), ',') FROM unnest(string_to_array(""FromBooks"", ',')) AS p WHERE p <> ''), '') WHERE ""FromBooks"" <> '';");
            migrationBuilder.Sql(@"UPDATE ""Trades"" SET ""ToBooks"" = COALESCE((SELECT string_agg(((split_part(p, ':', 1)::int / 3) * 5 + split_part(p, ':', 1)::int % 3)::text || ':' || split_part(p, ':', 2), ',') FROM unnest(string_to_array(""ToBooks"", ',')) AS p WHERE p <> ''), '') WHERE ""ToBooks"" <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to three skills a class: the fourth and fifth skills' grades and scrolls are dropped.
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""SkillGrades"" = concat_ws(';', split_part(""SkillGrades"", ';', 1), split_part(""SkillGrades"", ';', 2), split_part(""SkillGrades"", ';', 3), split_part(""SkillGrades"", ';', 6), split_part(""SkillGrades"", ';', 7), split_part(""SkillGrades"", ';', 8), split_part(""SkillGrades"", ';', 11), split_part(""SkillGrades"", ';', 12), split_part(""SkillGrades"", ';', 13), split_part(""SkillGrades"", ';', 16), split_part(""SkillGrades"", ';', 17), split_part(""SkillGrades"", ';', 18)) WHERE ""SkillGrades"" <> '';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""SkillProgress"" = concat_ws(';', split_part(""SkillProgress"", ';', 1), split_part(""SkillProgress"", ';', 2), split_part(""SkillProgress"", ';', 3), split_part(""SkillProgress"", ';', 6), split_part(""SkillProgress"", ';', 7), split_part(""SkillProgress"", ';', 8), split_part(""SkillProgress"", ';', 11), split_part(""SkillProgress"", ';', 12), split_part(""SkillProgress"", ';', 13), split_part(""SkillProgress"", ';', 16), split_part(""SkillProgress"", ';', 17), split_part(""SkillProgress"", ';', 18)) WHERE ""SkillProgress"" <> '';");
            migrationBuilder.Sql(@"UPDATE ""Accounts"" SET ""SkillReads"" = concat_ws(';', split_part(""SkillReads"", ';', 1), split_part(""SkillReads"", ';', 2), split_part(""SkillReads"", ';', 3), split_part(""SkillReads"", ';', 6), split_part(""SkillReads"", ';', 7), split_part(""SkillReads"", ';', 8), split_part(""SkillReads"", ';', 11), split_part(""SkillReads"", ';', 12), split_part(""SkillReads"", ';', 13), split_part(""SkillReads"", ';', 16), split_part(""SkillReads"", ';', 17), split_part(""SkillReads"", ';', 18)) WHERE ""SkillReads"" <> '';");
            migrationBuilder.Sql(@"DELETE FROM ""BookStacks"" WHERE ""BookId"" % 5 >= 3;");
            migrationBuilder.Sql(@"UPDATE ""BookStacks"" SET ""BookId"" = ""BookId"" + 1000;");
            migrationBuilder.Sql(@"UPDATE ""BookStacks"" SET ""BookId"" = ((""BookId"" - 1000) / 5) * 3 + (""BookId"" - 1000) % 5;");
            migrationBuilder.Sql(@"DELETE FROM ""MarketListings"" WHERE ""BookId"" >= 0 AND ""BookId"" % 5 >= 3;");
            migrationBuilder.Sql(@"UPDATE ""MarketListings"" SET ""BookId"" = (""BookId"" / 5) * 3 + ""BookId"" % 5 WHERE ""BookId"" >= 0;");
            migrationBuilder.Sql(@"UPDATE ""Trades"" SET ""FromBooks"" = COALESCE((SELECT string_agg(((split_part(p, ':', 1)::int / 5) * 3 + split_part(p, ':', 1)::int % 5)::text || ':' || split_part(p, ':', 2), ',') FROM unnest(string_to_array(""FromBooks"", ',')) AS p WHERE p <> '' AND split_part(p, ':', 1)::int % 5 < 3), '') WHERE ""FromBooks"" <> '';");
            migrationBuilder.Sql(@"UPDATE ""Trades"" SET ""ToBooks"" = COALESCE((SELECT string_agg(((split_part(p, ':', 1)::int / 5) * 3 + split_part(p, ':', 1)::int % 5)::text || ':' || split_part(p, ':', 2), ',') FROM unnest(string_to_array(""ToBooks"", ',')) AS p WHERE p <> '' AND split_part(p, ':', 1)::int % 5 < 3), '') WHERE ""ToBooks"" <> '';");

            migrationBuilder.AlterColumn<string>(
                name: "SkillReads",
                table: "Accounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);
        }
    }
}
