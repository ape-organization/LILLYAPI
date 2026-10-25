using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyAPI.Migrations
{
    /// <inheritdoc />
    public partial class CatalogAndVisitIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_websiteVisits_VisitorId_VisitedAt",
                table: "websiteVisits",
                columns: new[] { "VisitorId", "VisitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsDeleted_CreatedAt_Id",
                table: "Products",
                columns: new[] { "IsDeleted", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_websiteVisits_VisitorId_VisitedAt",
                table: "websiteVisits");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsDeleted_CreatedAt_Id",
                table: "Products");
        }
    }
}
