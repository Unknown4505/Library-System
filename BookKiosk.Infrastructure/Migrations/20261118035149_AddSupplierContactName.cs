using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookKiosk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierContactName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "Suppliers");
        }
    }
}
