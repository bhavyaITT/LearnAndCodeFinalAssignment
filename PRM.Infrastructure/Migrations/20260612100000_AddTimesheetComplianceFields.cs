using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimesheetComplianceFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PendingMissedWeekStart",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimesheetReminderCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TimesheetSubmissionFrozen",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PendingMissedWeekStart", table: "Users");
            migrationBuilder.DropColumn(name: "TimesheetReminderCount", table: "Users");
            migrationBuilder.DropColumn(name: "TimesheetSubmissionFrozen", table: "Users");
        }
    }
}
