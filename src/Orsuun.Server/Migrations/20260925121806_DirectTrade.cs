using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class DirectTrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    FromItems = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    FromSorn = table.Column<long>(type: "bigint", nullable: false),
                    FromStep = table.Column<int>(type: "integer", nullable: false),
                    ToItems = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ToSorn = table.Column<long>(type: "bigint", nullable: false),
                    ToStep = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TouchedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_FromId_State",
                table: "Trades",
                columns: new[] { "FromId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_ToId_State",
                table: "Trades",
                columns: new[] { "ToId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Trades");
        }
    }
}
