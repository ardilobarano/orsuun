using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class KorstoneRain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RainDamage",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "RainFallId",
                table: "Accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "RainStrikes",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "KorstoneFalls",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Map = table.Column<int>(type: "integer", nullable: false),
                    Camp = table.Column<int>(type: "integer", nullable: false),
                    FellUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Heroes = table.Column<int>(type: "integer", nullable: false),
                    HpMax = table.Column<long>(type: "bigint", nullable: false),
                    HpLeft = table.Column<long>(type: "bigint", nullable: false),
                    BrokenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BrokenBy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Settled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KorstoneFalls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KorstoneStrikes",
                columns: table => new
                {
                    FallId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Damage = table.Column<long>(type: "bigint", nullable: false),
                    Strikes = table.Column<int>(type: "integer", nullable: false),
                    Utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KorstoneStrikes", x => new { x.FallId, x.AccountId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KorstoneFalls");

            migrationBuilder.DropTable(
                name: "KorstoneStrikes");

            migrationBuilder.DropColumn(
                name: "RainDamage",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "RainFallId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "RainStrikes",
                table: "Accounts");
        }
    }
}
