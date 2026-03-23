using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VRArcadeFinder.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumColumnsToArcade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PremiumExpiresAt",
                table: "Arcades",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PremiumStartedAt",
                table: "Arcades",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PremiumTier",
                table: "Arcades",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PremiumExpiresAt",
                table: "Arcades");

            migrationBuilder.DropColumn(
                name: "PremiumStartedAt",
                table: "Arcades");

            migrationBuilder.DropColumn(
                name: "PremiumTier",
                table: "Arcades");
        }
    }
}
