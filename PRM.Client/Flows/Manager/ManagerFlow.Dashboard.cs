using PRM.Application.DTOs;
using PRM.Application.DTOs.Manager;
using PRM.Client.Services;
using PRM.Client.UI;

namespace PRM.Client.Flows.Manager;

public sealed partial class ManagerFlow
{
    // ── Resource Dashboard ──────────────────────────────────────

    private async Task ResourceDashboardAsync(CancellationToken ct)
    {
        while (true)
        {
            var result = await api.GetResourceDashboardAsync(ct);
            if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }
            var data = result.Data!;

            ConsoleUi.ClearScreen();
            var monthYear = DateTime.Now.ToString("MMMM yyyy");
            ConsoleUi.WriteHeader($"RESOURCE DASHBOARD — {monthYear}");
            Console.WriteLine();
            Console.WriteLine($"ON BENCH  ({data.BenchCount} employees available)");
            ConsoleUi.WriteDivider();
            Console.WriteLine($"{"ID",-5} {"Name",-18} {"Department",-12} {"Skills"}");

            foreach (var e in data.BenchEmployees)
                Console.WriteLine($"{e.Id,-5} {e.FullName,-18} {e.Department,-12} {string.Join(", ", e.Skills)}");

            Console.WriteLine();
            Console.WriteLine("ACTIVE EMPLOYEES");
            ConsoleUi.WriteDivider();
            Console.WriteLine($"{"ID",-5} {"Name",-18} {"Alloc %",-9} {"Availability"}");

            foreach (var e in data.ActiveEmployees)
                Console.WriteLine($"{e.Id,-5} {e.FullName,-18} {e.TotalUtilisationPercent,4}%  {e.AvailabilityLabel}");

            ConsoleUi.WriteDivider();
            Console.WriteLine($"Bench: {data.BenchCount}   |   Partial: {data.PartialCount}");
            Console.WriteLine("[D] Drill into employee details     [B] Back");

            var action = ConsoleUi.ReadLine("Choice: ").ToUpperInvariant();
            if (action == "B") return;
            if (action == "D")
                await EmployeeDrillInAsync(ct);
        }
    }

    private async Task EmployeeDrillInAsync(CancellationToken ct)
    {
        var idText = ConsoleUi.ReadRequired("Enter Employee ID: ");
        if (!int.TryParse(idText, out var id)) { ConsoleUi.ShowError("Invalid ID."); ConsoleUi.Pause(); return; }

        var result = await api.GetEmployeeDrillInAsync(id, ct);
        if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }
        var e = result.Data!;

        ConsoleUi.ClearScreen();
        Console.WriteLine($"── {e.FullName} ─────────────────────────────────");
        Console.WriteLine($"Department     : {e.Department}");
        Console.WriteLine($"Current Status : {e.Status.ToUpperInvariant()} ({e.TotalUtilisationPercent}%)");
        Console.WriteLine($"Profile Skills : {string.Join(", ", e.ProfileSkills)}");
        Console.WriteLine();
        Console.WriteLine("Active Allocations:");
        Console.WriteLine($"  {"Project",-16} {"%",-6} {"From",-12} {"To"}");
        foreach (var a in e.ActiveAllocations)
            Console.WriteLine($"  {a.ProjectName,-16} {a.UtilisationPercent,4}%  {ConsoleUi.FormatDate(a.FromDate),-12} {ConsoleUi.FormatDate(a.ToDate)}");

        Console.WriteLine();
        Console.WriteLine("Recent Activity Tags (last 4 weeks):");
        Console.WriteLine($"  {string.Join(", ", e.RecentActivityTags)}");
        Console.WriteLine();
        Console.WriteLine("[B] Back");
        ConsoleUi.ReadLine("");
    }
}

