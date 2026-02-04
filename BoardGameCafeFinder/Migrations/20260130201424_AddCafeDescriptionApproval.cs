using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardGameCafeFinder.Migrations
{
    /// <inheritdoc />
    public partial class AddCafeDescriptionApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDescriptionApproved",
                table: "Cafes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDescriptionApproved",
                table: "Cafes");
        }
    }
}
