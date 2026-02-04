using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardGameCafeFinder.Migrations
{
    /// <inheritdoc />
    public partial class AddCityAndCrawlHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    CityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "US"),
                    CrawlCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastCrawledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCrawlStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NextCrawlAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MaxResults = table.Column<int>(type: "int", nullable: false, defaultValue: 15),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.CityId);
                });

            migrationBuilder.CreateTable(
                name: "CrawlHistories",
                columns: table => new
                {
                    CrawlHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "InProgress"),
                    CafesFound = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CafesAdded = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CafesUpdated = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlHistories", x => x.CrawlHistoryId);
                    table.ForeignKey(
                        name: "FK_CrawlHistories_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cities_CrawlCount",
                table: "Cities",
                column: "CrawlCount");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_IsActive",
                table: "Cities",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_LastCrawledAt",
                table: "Cities",
                column: "LastCrawledAt");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_Name",
                table: "Cities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_NextCrawlAt",
                table: "Cities",
                column: "NextCrawlAt");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_Region",
                table: "Cities",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlHistories_CityId",
                table: "CrawlHistories",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlHistories_StartedAt",
                table: "CrawlHistories",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlHistories_Status",
                table: "CrawlHistories",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrawlHistories");

            migrationBuilder.DropTable(
                name: "Cities");
        }
    }
}
