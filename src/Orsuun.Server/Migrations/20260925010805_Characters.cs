using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class Characters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Characters (25 Sep 2026): every existing hero becomes the first character of its own login (same id). The
            // login takes its email and password, Amber, packs bought and Banner; its devices and Google / Apple links
            // point at the login. Names are filled at startup (C#, GameService.BackfillNamesAsync).
            migrationBuilder.CreateTable(
                name: "Logins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Amber = table.Column<long>(type: "bigint", nullable: false),
                    AmberPurchases = table.Column<int>(type: "integer", nullable: false),
                    Banner = table.Column<int>(type: "integer", nullable: false),
                    SwornUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logins", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(name: "LoginId", table: "Accounts", type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.AddColumn<int>(name: "Slot", table: "Accounts", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "Name", table: "Accounts", type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "NameKey", table: "Accounts", type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<Guid>(name: "LoginId", table: "Devices", type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.AddColumn<Guid>(name: "DepotLoginId", table: "Items", type: "uuid", nullable: true);

            migrationBuilder.Sql(@"
INSERT INTO ""Logins"" (""Id"", ""CreatedUtc"", ""CreatedIp"", ""Email"", ""PasswordHash"", ""Amber"", ""AmberPurchases"", ""Banner"", ""SwornUtc"")
SELECT ""Id"", ""CreatedUtc"", ""CreatedIp"", ""Email"", ""PasswordHash"", ""Amber"", ""AmberPurchases"", ""Banner"", ""SwornUtc"" FROM ""Accounts"";
UPDATE ""Accounts"" SET ""LoginId"" = ""Id"", ""Slot"" = 0;
UPDATE ""Devices"" SET ""LoginId"" = ""AccountId"";");

            // Links pointed at the account, whose id is now its login's id.
            migrationBuilder.DropIndex(name: "IX_ExternalLogins_AccountId", table: "ExternalLogins");
            migrationBuilder.RenameColumn(name: "AccountId", table: "ExternalLogins", newName: "LoginId");

            migrationBuilder.DropIndex(name: "IX_Accounts_Email", table: "Accounts");
            migrationBuilder.DropColumn(name: "Email", table: "Accounts");
            migrationBuilder.DropColumn(name: "PasswordHash", table: "Accounts");
            migrationBuilder.DropColumn(name: "Amber", table: "Accounts");
            migrationBuilder.DropColumn(name: "AmberPurchases", table: "Accounts");

            migrationBuilder.CreateIndex(name: "IX_ExternalLogins_LoginId", table: "ExternalLogins", column: "LoginId");
            migrationBuilder.CreateIndex(name: "IX_Items_DepotLoginId", table: "Items", column: "DepotLoginId");
            migrationBuilder.CreateIndex(name: "IX_Devices_LoginId", table: "Devices", column: "LoginId");
            migrationBuilder.CreateIndex(name: "IX_Accounts_LoginId", table: "Accounts", column: "LoginId");
            migrationBuilder.CreateIndex(name: "IX_Accounts_NameKey", table: "Accounts", column: "NameKey", unique: true, filter: "\"NameKey\" <> ''");
            migrationBuilder.CreateIndex(name: "IX_Logins_CreatedIp_CreatedUtc", table: "Logins", columns: new[] { "CreatedIp", "CreatedUtc" });
            migrationBuilder.CreateIndex(name: "IX_Logins_Email", table: "Logins", column: "Email", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to one hero per account: slot 0 keeps its login's email, Amber and packs; links go to it.
            migrationBuilder.AddColumn<string>(name: "Email", table: "Accounts", type: "character varying(254)", maxLength: 254, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PasswordHash", table: "Accounts", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<long>(name: "Amber", table: "Accounts", type: "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<int>(name: "AmberPurchases", table: "Accounts", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.Sql(@"
UPDATE ""Accounts"" a SET ""Email"" = l.""Email"", ""PasswordHash"" = l.""PasswordHash"", ""Amber"" = l.""Amber"", ""AmberPurchases"" = l.""AmberPurchases""
FROM ""Logins"" l WHERE l.""Id"" = a.""LoginId"" AND a.""Id"" = (SELECT b.""Id"" FROM ""Accounts"" b WHERE b.""LoginId"" = l.""Id"" ORDER BY b.""Slot"" LIMIT 1);
UPDATE ""ExternalLogins"" x SET ""LoginId"" = (SELECT b.""Id"" FROM ""Accounts"" b WHERE b.""LoginId"" = x.""LoginId"" ORDER BY b.""Slot"" LIMIT 1);");
            migrationBuilder.DropIndex(name: "IX_ExternalLogins_LoginId", table: "ExternalLogins");
            migrationBuilder.RenameColumn(name: "LoginId", table: "ExternalLogins", newName: "AccountId");
            migrationBuilder.CreateIndex(name: "IX_ExternalLogins_AccountId", table: "ExternalLogins", column: "AccountId");
            migrationBuilder.CreateIndex(name: "IX_Accounts_Email", table: "Accounts", column: "Email", unique: true);
            migrationBuilder.DropIndex(name: "IX_Items_DepotLoginId", table: "Items");
            migrationBuilder.DropIndex(name: "IX_Devices_LoginId", table: "Devices");
            migrationBuilder.DropIndex(name: "IX_Accounts_LoginId", table: "Accounts");
            migrationBuilder.DropIndex(name: "IX_Accounts_NameKey", table: "Accounts");
            migrationBuilder.DropColumn(name: "DepotLoginId", table: "Items");
            migrationBuilder.DropColumn(name: "LoginId", table: "Devices");
            migrationBuilder.DropColumn(name: "LoginId", table: "Accounts");
            migrationBuilder.DropColumn(name: "Slot", table: "Accounts");
            migrationBuilder.DropColumn(name: "Name", table: "Accounts");
            migrationBuilder.DropColumn(name: "NameKey", table: "Accounts");
            migrationBuilder.DropTable(name: "Logins");
        }
    }
}
