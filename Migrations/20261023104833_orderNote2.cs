using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyAPI.Migrations
{
    /// <inheritdoc />
    public partial class orderNote2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "OrderItems");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "OrderItems",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
