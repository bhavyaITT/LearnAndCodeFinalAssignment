using PRM.Application.DTOs;
using PRM.Application.DTOs.Allocation;
using PRM.Application.DTOs.Employee;
using PRM.Application.DTOs.Project;
using PRM.Application.DTOs.Users;
using PRM.Client.Services;
using PRM.Client.UI;

namespace PRM.Client.Flows.Admin;

public sealed partial class AdminFlow
{
    // ── Allocations ─────────────────────────────────────────────

    private async Task ViewAllocationsAsync(CancellationToken ct)
    {
        int? employeeFilter = null;
        int? projectFilter = null;

        while (true)
        {
            var result = await api.GetAllocationsAsync(employeeFilter, projectFilter, true, ct);
            if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }

            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("ALL ALLOCATIONS");
            Console.WriteLine($"{"Emp ID",-7} {"Employee",-16} {"Project",-18} {"%",-6} {"From",-12} {"To"}");
            ConsoleUi.WriteDivider();

            foreach (var a in result.Data!.Allocations)
                Console.WriteLine($"{a.EmployeeId,-7} {a.EmployeeName,-16} {a.ProjectName,-18} {a.UtilisationPercentage,4}%  {ConsoleUi.FormatDate(a.FromDate),-12} {ConsoleUi.FormatDate(a.ToDate)}");

            ConsoleUi.WriteDivider();
            Console.WriteLine($"Total Active Allocations: {result.Data.TotalActiveCount}");
            Console.WriteLine("[F] Filter by Employee / Project     [B] Back");

            var action = ConsoleUi.ReadLine("Choice: ").ToUpperInvariant();
            if (action == "B") return;
            if (action == "F")
            {
                var empInput = ConsoleUi.ReadLine("Employee ID or Enter to clear: ");
                employeeFilter = int.TryParse(empInput, out var eid) ? eid : null;
                var projInput = ConsoleUi.ReadLine("Project ID or Enter to clear: ");
                projectFilter = int.TryParse(projInput, out var pid) ? pid : null;
            }
        }
    }
}

