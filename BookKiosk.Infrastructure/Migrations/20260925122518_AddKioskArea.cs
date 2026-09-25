using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookKiosk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKioskArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AreaId",
                table: "Kiosks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Kiosks_AreaId",
                table: "Kiosks",
                column: "AreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Kiosks_Areas_AreaId",
                table: "Kiosks",
                column: "AreaId",
                principalTable: "Areas",
                principalColumn: "AreaId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Kiosks_Areas_AreaId",
                table: "Kiosks");

            migrationBuilder.DropIndex(
                name: "IX_Kiosks_AreaId",
                table: "Kiosks");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "Kiosks");
        }
    }
}
