using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orsuun.Server.Migrations
{
    /// <inheritdoc />
    public partial class ExchangeGoods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GoodCount",
                table: "MarketListings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // -1: no goods (a piece or a scroll stack); 0 is the Draught.
            migrationBuilder.AddColumn<int>(
                name: "GoodId",
                table: "MarketListings",
                type: "integer",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_Status_ClosedUtc",
                table: "MarketListings",
                columns: new[] { "Status", "ClosedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketListings_Status_ClosedUtc",
                table: "MarketListings");

            migrationBuilder.DropColumn(
                name: "GoodCount",
                table: "MarketListings");

            migrationBuilder.DropColumn(
                name: "GoodId",
                table: "MarketListings");
        }
    }
}
