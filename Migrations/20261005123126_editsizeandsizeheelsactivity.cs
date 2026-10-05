using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyAPI.Migrations
{
    /// <inheritdoc />
    public partial class editsizeandsizeheelsactivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Sizes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "HeelSizes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsDeleted_CategoryId_Id",
                table: "Products",
                columns: new[] { "IsDeleted", "CategoryId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsDeleted_DiscountPercentage_Id",
                table: "Products",
                columns: new[] { "IsDeleted", "DiscountPercentage", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_IsDeleted_CategoryId_Id",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsDeleted_DiscountPercentage_Id",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Sizes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "HeelSizes");
        }
    }
}
