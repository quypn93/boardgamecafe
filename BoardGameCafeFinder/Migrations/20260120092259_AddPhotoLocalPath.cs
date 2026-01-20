using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardGameCafeFinder.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoLocalPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalPath",
                table: "Photos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalPath",
                table: "Photos");
        }
    }
}
