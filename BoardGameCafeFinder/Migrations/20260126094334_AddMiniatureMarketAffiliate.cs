using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardGameCafeFinder.Migrations
{
    /// <inheritdoc />
    public partial class AddMiniatureMarketAffiliate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MiniatureMarketAffiliateUrl",
                table: "BoardGames",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MiniatureMarketAffiliateUrl",
                table: "BoardGames");
        }
    }
}
