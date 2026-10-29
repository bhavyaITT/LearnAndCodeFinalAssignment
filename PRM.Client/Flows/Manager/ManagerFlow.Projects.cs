using PRM.Application.DTOs;
using PRM.Application.DTOs.Manager;
using PRM.Client.Services;
using PRM.Client.UI;

namespace PRM.Client.Flows.Manager;

public sealed partial class ManagerFlow
{
    // ── My Projects ─────────────────────────────────────────────

    private async Task MyProjectsAsync(CancellationToken ct)
    {
        var projects = await api.GetMyProjectsAsync(ct);
        if (!projects.IsSuccess) { ConsoleUi.ShowApiError(projects.Error); return; }
        var list = projects.Data!;

        if (list.Count == 0) { ConsoleUi.ShowError("No projects found."); ConsoleUi.Pause(); return; }

        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("MY PROJECTS");
        Console.WriteLine($"{"#",-4} {"Project",-18} {"End Date",-12} {"Health"}");
        ConsoleUi.WriteDivider();

        for (var i = 0; i < list.Count; i++)
            Console.WriteLine($"{i + 1}.  {list[i].Name,-16} {ConsoleUi.FormatDate(list[i].EndDate),-12} {ConsoleUi.HealthWithEmoji(list[i].Health)}");

        ConsoleUi.WriteDivider();
        var choice = ConsoleUi.ReadMenuOption("Select project number to view details: ", 1, list.Count);
        await ProjectDetailAsync(list[choice - 1].Id, ct);
    }

    private async Task ProjectDetailAsync(int projectId, CancellationToken ct)
    {
        while (true)
        {
            var result = await api.GetMyProjectDetailAsync(projectId, ct);
            if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }
            var p = result.Data!;

            ConsoleUi.ClearScreen();
            Console.WriteLine($"── {p.Name} ───────────────────────────────");
            Console.WriteLine($"Health Status : {ConsoleUi.HealthWithEmoji(p.Health)}");
            Console.WriteLine();
            Console.WriteLine("Risk Flags:");
            foreach (var f in p.RiskFlags)
                Console.WriteLine($"  {(f.IsCritical ? "[X]" : "[OK]")}  {f.Message}");

            Console.WriteLine();
            Console.WriteLine("Milestones:");
            Console.WriteLine($"  {"#",-4} {"Title",-18} {"Due Date",-12} {"Status"}");
            var milestones = p.Milestones.ToList();
            var today = DateOnly.FromDateTime(DateTime.Today);
            for (var i = 0; i < milestones.Count; i++)
            {
                var m = milestones[i];
                var overdue = !m.Status.Equals("Done", StringComparison.OrdinalIgnoreCase) && m.DueDate < today;
                Console.WriteLine($"  {i + 1}.  {m.Title,-16} {ConsoleUi.FormatDate(m.DueDate),-12} {m.Status}{(overdue ? "  OVERDUE" : "")}");
            }

            Console.WriteLine();
            Console.WriteLine("Allocated Resources:");
            Console.WriteLine($"  {"Name",-14} {"%",-6} {"From",-12} {"To"}");
            foreach (var a in p.AllocatedResources)
                Console.WriteLine($"  {a.EmployeeName,-14} {a.UtilisationPercent,4}%  {ConsoleUi.FormatDate(a.FromDate),-12} {ConsoleUi.FormatDate(a.ToDate)}");

            ConsoleUi.WriteDivider();
            Console.WriteLine("[A] Get AI Risk Summary     [B] Back");

            var action = ConsoleUi.ReadLine("Choice: ").ToUpperInvariant();
            if (action == "B") return;
            if (action == "A")
                await ShowRiskSummaryAsync(projectId, ct);
        }
    }

    private async Task ShowRiskSummaryAsync(int projectId, CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("Generating AI summary...");
        var result = await api.RiskSummaryAsync(projectId, ct);
        if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }

        ConsoleUi.WriteDivider();
        Console.WriteLine($"AI Risk Summary — {result.Data!.ProjectName}");
        ConsoleUi.WriteDivider();
        Console.WriteLine();
        Console.WriteLine($"\"{result.Data.Summary}\"");
        Console.WriteLine();
        Console.WriteLine($"  Note: {result.Data.AiNote}");
        ConsoleUi.Pause();
    }
}

