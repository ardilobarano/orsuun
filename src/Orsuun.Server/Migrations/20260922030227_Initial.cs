using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SessionToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WeaponsBroken = table.Column<int>(type: "integer", nullable: false),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sorn = table.Column<long>(type: "bigint", nullable: false),
                    Potions = table.Column<int>(type: "integer", nullable: false),
                    Materials = table.Column<int>(type: "integer", nullable: false),
                    ScrollsOfMercy = table.Column<int>(type: "integer", nullable: false),
                    KhansAlloys = table.Column<int>(type: "integer", nullable: false),
                    AnvilWards = table.Column<int>(type: "integer", nullable: false),
                    Turnstones = table.Column<int>(type: "integer", nullable: false),
                    EtchingNeedles = table.Column<int>(type: "integer", nullable: false),
                    SummoningMarkers = table.Column<int>(type: "integer", nullable: false),
                    Xp = table.Column<long>(type: "bigint", nullable: false),
                    Korshards = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Skins = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    HighestStageCleared = table.Column<int>(type: "integer", nullable: false),
                    ParkedStage = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BossClocks",
                columns: table => new
                {
                    BossId = table.Column<int>(type: "integer", nullable: false),
                    SpawnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BossClocks", x => x.BossId);
                });

            migrationBuilder.CreateTable(
                name: "Ledger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SornDelta = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ledger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    Equipped = table.Column<bool>(type: "boolean", nullable: false),
                    ItemLevel = table.Column<int>(type: "integer", nullable: false),
                    Rarity = table.Column<int>(type: "integer", nullable: false),
                    UpgradeLevel = table.Column<int>(type: "integer", nullable: false),
                    PatienceBp = table.Column<int>(type: "integer", nullable: false),
                    LockedEtchingIndex = table.Column<int>(type: "integer", nullable: false),
                    Etchings = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Sockets = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Destroyed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Accounts_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_DeviceToken",
                table: "Accounts",
                column: "DeviceToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SessionToken",
                table: "Accounts",
                column: "SessionToken");

            migrationBuilder.CreateIndex(
                name: "IX_Items_OwnerId",
                table: "Items",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Ledger_AccountId_RequestId",
                table: "Ledger",
                columns: new[] { "AccountId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ledger_AccountId_Utc",
                table: "Ledger",
                columns: new[] { "AccountId", "Utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BossClocks");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "Ledger");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
