using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyAPI.Migrations
{
    /// <inheritdoc />
    public partial class indexes2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sizes_Name",
                table: "Sizes");

            migrationBuilder.DropIndex(
                name: "IX_HeelSizes_Name",
                table: "HeelSizes");

            migrationBuilder.CreateIndex(
                name: "IX_Sizes_Name",
                table: "Sizes",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_HeelSizes_Name",
                table: "HeelSizes",
                column: "Name",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sizes_Name",
                table: "Sizes");

            migrationBuilder.DropIndex(
                name: "IX_HeelSizes_Name",
                table: "HeelSizes");

            migrationBuilder.CreateIndex(
                name: "IX_Sizes_Name",
                table: "Sizes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeelSizes_Name",
                table: "HeelSizes",
                column: "Name",
                unique: true);
        }
    }
}
