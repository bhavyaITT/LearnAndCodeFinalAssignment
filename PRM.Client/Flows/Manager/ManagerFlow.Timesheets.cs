using PRM.Application.DTOs;
using PRM.Application.DTOs.Manager;
using PRM.Client.Services;
using PRM.Client.UI;

namespace PRM.Client.Flows.Manager;

public sealed partial class ManagerFlow
{
    // ── Team Timesheets ─────────────────────────────────────────

    private async Task TeamTimesheetsAsync(CancellationToken ct)
    {
        while (true)
        {
            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("TIMESHEETS — MY TEAM");
            var weekInput = ConsoleUi.ReadLine("Filter by week (DD-MM-YYYY) or press Enter for current week: ");
            DateOnly? week = null;
            if (!string.IsNullOrWhiteSpace(weekInput) && ConsoleUi.TryParseDate(weekInput, out var parsed))
                week = parsed;

            var result = await api.GetTeamTimesheetsAsync(week, ct);
            if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }
            var data = result.Data!;

            Console.WriteLine();
            Console.WriteLine($"Week: {ConsoleUi.FormatDate(data.WeekStartDate)}");
            ConsoleUi.WriteDivider();
            Console.WriteLine($"{"ID",-6} {"Employee",-16} {"Project",-16} {"Hrs",-6} {"Status"}");
            ConsoleUi.WriteDivider();

            foreach (var e in data.Entries)
            {
                var status = e.Status.Equals("Missed", StringComparison.OrdinalIgnoreCase)
                    ? "MISSED !"
                    : e.Status.ToUpperInvariant();
                Console.WriteLine($"{e.EmployeeId,-6} {e.EmployeeName,-16} {e.ProjectName,-16} {e.HoursWorked,-6} {status}");
            }

            ConsoleUi.WriteDivider();
            Console.WriteLine("[V] View employee timesheet detail");
            Console.WriteLine("[R] Restore frozen submission access");
            Console.WriteLine("[B] Back");

            var action = ConsoleUi.ReadLine("Choice: ").ToUpperInvariant();
            if (action == "B") return;
            if (action == "R")
                await RestoreFrozenSubmissionAsync(ct);
            if (action == "V")
                await TeamTimesheetDetailAsync(data.WeekStartDate, ct);
        }
    }

    private async Task RestoreFrozenSubmissionAsync(CancellationToken ct)
    {
        var empIdText = ConsoleUi.ReadRequired("Enter Employee ID to restore: ");
        if (!int.TryParse(empIdText, out var employeeId))
        {
            ConsoleUi.ShowError("Invalid ID.");
            ConsoleUi.Pause();
            return;
        }

        var result = await api.RestoreTimesheetSubmissionAsync(employeeId, ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("Submission access restored."); ConsoleUi.Pause(); }
    }

    private async Task TeamTimesheetDetailAsync(DateOnly weekStart, CancellationToken ct)
    {
        var empIdText = ConsoleUi.ReadRequired("Enter Employee ID: ");
        if (!int.TryParse(empIdText, out var employeeId)) { ConsoleUi.ShowError("Invalid ID."); ConsoleUi.Pause(); return; }

        var result = await api.GetTeamTimesheetDetailAsync(employeeId, weekStart, ct);
        if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }
        var d = result.Data!;

        ConsoleUi.ClearScreen();
        Console.WriteLine($"── {d.EmployeeName} — Week: {ConsoleUi.FormatDate(d.WeekStartDate)} — {d.Status.ToUpperInvariant()} ─────");
        Console.WriteLine($"{"Project",-16} {"Hrs",-6} {"Activity Tags"}");
        ConsoleUi.WriteDivider();

        foreach (var p in d.Projects)
            Console.WriteLine($"{p.ProjectName,-16} {p.HoursWorked,-6} {string.Join(", ", p.ActivityTags)}");

        Console.WriteLine($"Total: {d.TotalHours} hrs");
        ConsoleUi.Pause();
    }
}

