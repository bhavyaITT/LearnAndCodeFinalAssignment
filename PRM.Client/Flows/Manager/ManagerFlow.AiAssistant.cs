using PRM.Application.DTOs;
using PRM.Application.DTOs.Manager;
using PRM.Client.Services;
using PRM.Client.UI;

namespace PRM.Client.Flows.Manager;

public sealed partial class ManagerFlow
{
    // ── AI Assistant ────────────────────────────────────────────

    private async Task AiAssistantAsync(CancellationToken ct)
    {
        while (true)
        {
            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("AI ASSISTANT");
            Console.WriteLine("1. Skill Match      — Find best employee for one requirement");
            Console.WriteLine("2. Team Staffing    — Staff all roles from bench in one search");
            Console.WriteLine("3. Risk Summary     — Get a health analysis for a project");
            Console.WriteLine("4. Back");
            Console.WriteLine();

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 4))
            {
                case 1: await AiSkillMatchAsync(ct); break;
                case 2: await AiTeamStaffingAsync(ct); break;
                case 3: await AiRiskSummaryMenuAsync(ct); break;
                case 4: return;
            }
        }
    }

    private async Task AiSkillMatchAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        Console.WriteLine("── Skill Match ────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine("Describe your project requirement in plain English:");
        var requirement = ConsoleUi.ReadRequired("> ");

        Console.WriteLine();
        Console.WriteLine("Searching... (calling AI)");
        var result = await api.SkillMatchAsync(new SkillMatchRequest(requirement), ct);
        if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }

        var candidates = result.Data!.Candidates.ToList();
        Console.WriteLine();
        Console.WriteLine("Results:");
        PrintSkillMatchCandidates(candidates);

        Console.WriteLine("  Note: These are AI-generated suggestions. Always verify availability");
        Console.WriteLine("  and skills with the employee before allocating.");
        Console.WriteLine("[A] Go to Allocate Resource     [B] Back");

        var action = ConsoleUi.ReadLine("Choice: ").ToUpperInvariant();
        if (action == "A")
            await AllocateResourceAsync(ct);
        else
            ConsoleUi.Pause();
    }

    private async Task AiTeamStaffingAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("TEAM STAFFING");
        Console.WriteLine("Describe the team you need in plain English.");
        Console.WriteLine("Example: 5-person squad for React + .NET microservices with QA and a scrum master.");
        Console.WriteLine();
        var requirement = ConsoleUi.ReadRequired("> ");
        var projectContext = ConsoleUi.ReadLine("Project context (optional): ");

        Console.WriteLine();
        Console.WriteLine("Searching employees... (AI matching in progress)");
        var result = await api.TeamStaffingAsync(
            new TeamStaffingRequest(
                requirement,
                string.IsNullOrWhiteSpace(projectContext) ? null : projectContext),
            ct);

        if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }

        var data = result.Data!;
        ConsoleUi.WriteDivider();
        Console.WriteLine($"BENCH REVIEWED: {data.BenchCandidatesConsidered}  |  ALLOCATED REVIEWED: {data.AllocatedCandidatesConsidered}");
        Console.WriteLine();

        Console.WriteLine("SUGGESTED MATCHES (BENCH)");
        ConsoleUi.WriteDivider();
        if (data.Matches.Any())
        {
            foreach (var m in data.Matches)
            {
                Console.WriteLine($"  {m.RoleTitle}");
                Console.WriteLine($"    -> {m.EmployeeName} (ID {m.EmployeeId})");
                Console.WriteLine($"       {m.MatchReason}");
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine("  (no bench matches)");
            Console.WriteLine();
        }

        if (data.AllocatedAlternatives.Any())
        {
            Console.WriteLine("SKILLED BUT ALREADY ALLOCATED");
            ConsoleUi.WriteDivider();
            foreach (var a in data.AllocatedAlternatives)
            {
                Console.WriteLine($"  {a.RoleTitle}");
                Console.WriteLine($"    -> {a.EmployeeName} (ID {a.EmployeeId})");
                Console.WriteLine($"       Project: {a.ProjectName}");
                Console.WriteLine($"       Allocated: {ConsoleUi.FormatDate(a.AllocatedFrom)} to {ConsoleUi.FormatDate(a.AllocatedTo)}");
                Console.WriteLine($"       {a.Reason}");
                Console.WriteLine();
            }
        }

        if (data.Gaps.Any())
        {
            Console.WriteLine("GAPS");
            ConsoleUi.WriteDivider();
            foreach (var g in data.Gaps)
            {
                var gapLabel = g.GapType.Equals("AvailabilityGap", StringComparison.OrdinalIgnoreCase)
                    ? "AVAILABILITY GAP"
                    : "SKILL GAP";
                Console.WriteLine($"  {g.RoleTitle} — {gapLabel}");
                Console.WriteLine($"    {g.Reason}");
                if (!string.IsNullOrWhiteSpace(g.AllocatedEmployeeName))
                    Console.WriteLine($"    Skilled employee: {g.AllocatedEmployeeName}");
                if (g.AllocatedFrom.HasValue && g.AllocatedTo.HasValue)
                    Console.WriteLine($"    Allocated: {ConsoleUi.FormatDate(g.AllocatedFrom.Value)} to {ConsoleUi.FormatDate(g.AllocatedTo.Value)}");
                if (!string.IsNullOrWhiteSpace(g.AllocatedProjectName))
                    Console.WriteLine($"    Project: {g.AllocatedProjectName}");
                if (g.NextAvailableDate.HasValue)
                    Console.WriteLine($"    Earliest availability: {ConsoleUi.FormatDate(g.NextAvailableDate.Value)}");
                Console.WriteLine();
            }
        }

        Console.WriteLine($"Note: {data.AiNote}");
        ConsoleUi.Pause();
    }

    private async Task AiRiskSummaryMenuAsync(CancellationToken ct)
    {
        var projects = await api.GetMyProjectsAsync(ct);
        if (!projects.IsSuccess) { ConsoleUi.ShowApiError(projects.Error); return; }
        var list = projects.Data!;

        ConsoleUi.ClearScreen();
        Console.WriteLine("── Risk Summary ───────────────────────────────");
        Console.WriteLine();
        Console.WriteLine("Select project:");
        for (var i = 0; i < list.Count; i++)
            Console.WriteLine($"  {i + 1}.  {list[i].Name}    {ConsoleUi.HealthWithEmoji(list[i].Health)}");

        var choice = ConsoleUi.ReadMenuOption("Enter project number: ", 1, list.Count);
        await ShowRiskSummaryAsync(list[choice - 1].Id, ct);
    }

    private static void PrintSkillMatchCandidates(IReadOnlyList<SkillMatchCandidateResponse> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            Console.WriteLine($"{i + 1}.  {c.FullName,-16} {c.FreeHoursPerWeek,2} hrs free/week");
            Console.WriteLine($"    \"{c.AiReason}\"");
            if (c.SuggestedAllocationPercent.HasValue)
            {
                Console.WriteLine(
                    $"    Suggested allocation: {c.SuggestedAllocationPercent}%  " +
                    $"(≈ {c.SuggestedAllocationPercent * c.FreeHoursPerWeek / Math.Max(c.FreeCapacityPercent, 1)} hrs/week)");
            }
            Console.WriteLine();
        }
    }
}

