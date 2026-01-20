using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardGameCafeFinder.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceImageUrlWithLocalImagePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Cafes");

            migrationBuilder.AddColumn<string>(
                name: "LocalImagePath",
                table: "Cafes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalImagePath",
                table: "Cafes");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Cafes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
