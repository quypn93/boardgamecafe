using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardGameCafeFinder.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryToBoardGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "BoardGames",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "BoardGames");
        }
    }
}
