using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class PartyDungeons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PartyDungeonId",
                table: "DungeonRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartyDungeons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyLeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DungeonId = table.Column<int>(type: "integer", nullable: false),
                    OpenedById = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedByName = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    OpenedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosesUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyDungeons", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartyDungeons");

            migrationBuilder.DropColumn(
                name: "PartyDungeonId",
                table: "DungeonRuns");
        }
    }
}
