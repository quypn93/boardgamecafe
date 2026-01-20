using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardGameCafeFinder.Migrations
{
    /// <inheritdoc />
    public partial class AddCafeBggFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BggUsername",
                table: "Cafes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LibraryUrl",
                table: "Cafes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cafes_BggUsername",
                table: "Cafes",
                column: "BggUsername");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cafes_BggUsername",
                table: "Cafes");

            migrationBuilder.DropColumn(
                name: "BggUsername",
                table: "Cafes");

            migrationBuilder.DropColumn(
                name: "LibraryUrl",
                table: "Cafes");
        }
    }
}
