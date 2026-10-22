using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookKiosk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductDiscountV71Snapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PointsUsedAmountAtTime",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionNameAtTime",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromotionTypeAtTime",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BookTitleAtTime",
                table: "OrderDetails",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPerUnitAtTime",
                table: "OrderDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsUsedAmountAtTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PromotionNameAtTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PromotionTypeAtTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BookTitleAtTime",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "DiscountPerUnitAtTime",
                table: "OrderDetails");
        }
    }
}
