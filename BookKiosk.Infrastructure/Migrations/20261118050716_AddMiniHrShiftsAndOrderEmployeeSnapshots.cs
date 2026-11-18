using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookKiosk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMiniHrShiftsAndOrderEmployeeSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmploymentTypeSnapshot",
                table: "SalaryPayments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRateSnapshot",
                table: "SalaryPayments",
                type: "decimal(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkedHoursSnapshot",
                table: "SalaryPayments",
                type: "decimal(7,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeCodeAtTime",
                table: "Orders",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNameAtTime",
                table: "Orders",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeShiftAssignmentId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KioskId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmploymentType",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "Employees",
                type: "decimal(18,0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkShifts",
                columns: table => new
                {
                    WorkShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkShifts", x => x.WorkShiftId);
                    table.CheckConstraint("CK_WorkShifts_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_WorkShifts_Users_FinalizedByUserId",
                        column: x => x.FinalizedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeShiftAssignments",
                columns: table => new
                {
                    AssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkShiftId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AssignedKioskId = table.Column<int>(type: "int", nullable: true),
                    WorkedHours = table.Column<decimal>(type: "decimal(7,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeShiftAssignments", x => x.AssignmentId);
                    table.CheckConstraint("CK_EmployeeShiftAssignments_WorkedHours", "[WorkedHours] IS NULL OR ([WorkedHours] >= 0 AND [WorkedHours] <= 744)");
                    table.ForeignKey(
                        name: "FK_EmployeeShiftAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftAssignments_Kiosks_AssignedKioskId",
                        column: x => x.AssignedKioskId,
                        principalTable: "Kiosks",
                        principalColumn: "KioskId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftAssignments_WorkShifts_WorkShiftId",
                        column: x => x.WorkShiftId,
                        principalTable: "WorkShifts",
                        principalColumn: "WorkShiftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalaryPayments_EmploymentTypeSnapshot",
                table: "SalaryPayments",
                sql: "[EmploymentTypeSnapshot] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalaryPayments_HourlySnapshot",
                table: "SalaryPayments",
                sql: "[WorkedHoursSnapshot] IS NULL OR [WorkedHoursSnapshot] >= 0 AND [HourlyRateSnapshot] IS NOT NULL AND [HourlyRateSnapshot] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_EmployeeShiftAssignmentId",
                table: "Orders",
                column: "EmployeeShiftAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_KioskId",
                table: "Orders",
                column: "KioskId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_EmploymentType",
                table: "Employees",
                sql: "[EmploymentType] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_HourlyRate",
                table: "Employees",
                sql: "[HourlyRate] IS NULL OR [HourlyRate] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftAssignments_AssignedKioskId",
                table: "EmployeeShiftAssignments",
                column: "AssignedKioskId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftAssignments_EmployeeId",
                table: "EmployeeShiftAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftAssignments_WorkShiftId_AssignedKioskId",
                table: "EmployeeShiftAssignments",
                columns: new[] { "WorkShiftId", "AssignedKioskId" },
                unique: true,
                filter: "[AssignedKioskId] IS NOT NULL AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftAssignments_WorkShiftId_EmployeeId",
                table: "EmployeeShiftAssignments",
                columns: new[] { "WorkShiftId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_FinalizedByUserId",
                table: "WorkShifts",
                column: "FinalizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_WorkDate_Status",
                table: "WorkShifts",
                columns: new[] { "WorkDate", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_EmployeeShiftAssignments_EmployeeShiftAssignmentId",
                table: "Orders",
                column: "EmployeeShiftAssignmentId",
                principalTable: "EmployeeShiftAssignments",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Kiosks_KioskId",
                table: "Orders",
                column: "KioskId",
                principalTable: "Kiosks",
                principalColumn: "KioskId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_EmployeeShiftAssignments_EmployeeShiftAssignmentId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Kiosks_KioskId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "EmployeeShiftAssignments");

            migrationBuilder.DropTable(
                name: "WorkShifts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalaryPayments_EmploymentTypeSnapshot",
                table: "SalaryPayments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalaryPayments_HourlySnapshot",
                table: "SalaryPayments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_EmployeeShiftAssignmentId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_KioskId",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_EmploymentType",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_HourlyRate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "EmploymentTypeSnapshot",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "HourlyRateSnapshot",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "WorkedHoursSnapshot",
                table: "SalaryPayments");

            migrationBuilder.DropColumn(
                name: "EmployeeCodeAtTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EmployeeNameAtTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EmployeeShiftAssignmentId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "KioskId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "Employees");
        }
    }
}
