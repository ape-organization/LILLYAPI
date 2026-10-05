using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyAPI.Migrations
{
    /// <inheritdoc />
    public partial class indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId_IsDeleted",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsDeleted",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ClientId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderDate",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_OrderId_ProductId",
                table: "OrderItems");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientId_OrderDate",
                table: "Orders",
                columns: new[] { "ClientId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderDate_Id",
                table: "Orders",
                columns: new[] { "OrderDate", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ClientId_OrderDate",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderDate_Id",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId_IsDeleted",
                table: "Products",
                columns: new[] { "CategoryId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsDeleted",
                table: "Products",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientId",
                table: "Orders",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderDate",
                table: "Orders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId_ProductId",
                table: "OrderItems",
                columns: new[] { "OrderId", "ProductId" });
        }
    }
}
