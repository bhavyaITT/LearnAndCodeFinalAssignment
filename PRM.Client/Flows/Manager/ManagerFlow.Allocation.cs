using PRM.Application.DTOs;
using PRM.Application.DTOs.Manager;
using PRM.Client.Services;
using PRM.Client.UI;

namespace PRM.Client.Flows.Manager;

public sealed partial class ManagerFlow
{
    // ── Allocate Resource ───────────────────────────────────────

    private async Task AllocateResourceAsync(CancellationToken ct)
    {
        while (true)
        {
            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("ALLOCATE RESOURCE");
            Console.WriteLine("1. Find resource using AI (recommended)");
            Console.WriteLine("2. Allocate directly (I already know who I want)");
            Console.WriteLine("3. End an existing allocation");
            Console.WriteLine("4. Back");
            Console.WriteLine();

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 4))
            {
                case 1: await AiAssistedAllocationAsync(ct); break;
                case 2: await DirectAllocationAsync(ct); break;
                case 3: await EndAllocationAsync(ct); break;
                case 4: return;
            }
        }
    }

    private async Task<int?> SelectProjectAsync(CancellationToken ct)
    {
        var projects = await api.GetMyProjectsAsync(ct);
        if (!projects.IsSuccess) { ConsoleUi.ShowApiError(projects.Error); return null; }
        var list = projects.Data!;
        if (list.Count == 0) { ConsoleUi.ShowError("No projects found."); ConsoleUi.Pause(); return null; }

        Console.WriteLine("Your projects:");
        for (var i = 0; i < list.Count; i++)
            Console.WriteLine($"  {list[i].Id}. {list[i].Name}");

        var input = ConsoleUi.ReadRequired("Enter project name or ID: ");
        var byId = list.FirstOrDefault(p => p.Id.ToString() == input);
        if (byId is not null) return byId.Id;

        var byName = list.FirstOrDefault(p => p.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
        return byName?.Id;
    }

    private async Task AiAssistedAllocationAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("ALLOCATE RESOURCE");
        Console.WriteLine("Step 1 — Select Project");
        var projectId = await SelectProjectAsync(ct);
        if (projectId is null) return;

        var projects = await api.GetMyProjectsAsync(ct);
        var projectName = projects.Data!.First(p => p.Id == projectId).Name;

        Console.WriteLine();
        Console.WriteLine("Step 2 — Describe your requirement");
        Console.WriteLine("Type what kind of resource you need:");
        var requirement = ConsoleUi.ReadRequired("> ");

        Console.WriteLine();
        Console.WriteLine("Searching... (AI matching in progress)");
        var match = await api.SkillMatchAsync(new SkillMatchRequest(requirement, projectName), ct);
        if (!match.IsSuccess) { ConsoleUi.ShowApiError(match.Error); return; }

        var candidates = match.Data!.Candidates.ToList();
        if (candidates.Count == 0)
        {
            ConsoleUi.ShowError("No matching employees found with sufficient availability.");
            ConsoleUi.Pause();
            return;
        }

        ConsoleUi.WriteDivider();
        Console.WriteLine("AI-MATCHED RESULTS");
        if (!string.IsNullOrWhiteSpace(match.Data!.RequirementText))
            Console.WriteLine($"(for: \"{match.Data.RequirementText}\")");
        ConsoleUi.WriteDivider();
        PrintSkillMatchCandidates(candidates);

        Console.WriteLine();
        Console.WriteLine("Note: Suggestions are AI-generated. Verify before confirming.");
        ConsoleUi.WriteDivider();

        var choice = ConsoleUi.ReadMenuOption("Select employee (enter #, or 0 to cancel): ", 0, candidates.Count);
        if (choice == 0) return;

        var selected = candidates[choice - 1];
        await ConfirmAllocationAsync(selected.EmployeeId, projectId.Value, selected.SuggestedAllocationPercent, ct);
    }

    private async Task DirectAllocationAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("DIRECT ALLOCATION");
        var projectId = await SelectProjectAsync(ct);
        if (projectId is null) return;

        var teamAlloc = await api.GetTeamEmployeeAllocationsAsync(ct);
        if (teamAlloc.IsSuccess && teamAlloc.Data!.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Your team — current allocations:");
            Console.WriteLine($"{"Emp ID",-7} {"Name",-14} {"Project",-14} {"%",-5} {"From",-12} {"To",-12} {"Today"}");
            ConsoleUi.WriteDivider();

            foreach (var row in teamAlloc.Data)
            {
                var projectName = row.ProjectName == "-" ? "(bench)" : row.ProjectName;
                var from = row.ProjectName == "-" ? "-" : ConsoleUi.FormatDate(row.FromDate);
                var to = row.ProjectName == "-" ? "-" : ConsoleUi.FormatDate(row.ToDate);
                var pct = row.ProjectName == "-" ? "-" : $"{row.UtilisationPercent}%";
                var todayFlag = row.IsActiveToday ? "Yes" : "No";

                Console.WriteLine($"{row.EmployeeId,-7} {row.EmployeeName,-14} {projectName,-14} {pct,-5} {from,-12} {to,-12} {todayFlag}");
            }

            ConsoleUi.WriteDivider();
            Console.WriteLine();
        }

        var empIdText = ConsoleUi.ReadRequired("Enter Employee ID : ");
        if (!int.TryParse(empIdText, out var employeeId)) { ConsoleUi.ShowError("Invalid ID."); ConsoleUi.Pause(); return; }

        await ConfirmAllocationAsync(employeeId, projectId.Value, null, ct);
    }

    private async Task ConfirmAllocationAsync(int employeeId, int projectId, int? suggestedPercent, CancellationToken ct)
    {
        var projectResult = await api.GetMyProjectDetailAsync(projectId, ct);
        if (!projectResult.IsSuccess) { ConsoleUi.ShowApiError(projectResult.Error); return; }
        var project = projectResult.Data!;

        var drill = await api.GetEmployeeDrillInAsync(employeeId, ct);
        if (!drill.IsSuccess) { ConsoleUi.ShowApiError(drill.Error); return; }
        var emp = drill.Data!;

        Console.WriteLine();
        Console.WriteLine($"── {emp.FullName} ─────────────────────────────────");
        Console.WriteLine($"Project period: {ConsoleUi.FormatDate(project.StartDate)} to {ConsoleUi.FormatDate(project.EndDate)}");
        Console.WriteLine($"Current Utilisation: {emp.TotalUtilisationPercent}%");

        var defaultPct = suggestedPercent ?? 50;
        var pct = ConsoleUi.ReadUtilisationPercent(defaultPct);
        var fromDate = ConsoleUi.ReadDateWithinRange(
            project.StartDate, project.EndDate, "From Date", notBeforeToday: true);
        var toDate = ConsoleUi.ReadEndDateWithinRange(
            fromDate, project.StartDate, project.EndDate, "To Date");

        var request = new CreateAllocationRequest(employeeId, projectId, pct, fromDate, toDate);

        Console.WriteLine();
        Console.WriteLine("Validating...");
        var validation = await api.ValidateAllocationAsync(request, ct);
        if (!validation.IsSuccess) { ConsoleUi.ShowApiError(validation.Error); return; }

        var v = validation.Data!;
        Console.WriteLine($"  {v.EmployeeName} total in this period: {v.ExistingUtilisationInPeriod}% + {v.RequestedUtilisation}% = {v.TotalIfConfirmed}%   {(v.IsValid ? "Valid" : "INVALID")}");
        if (!string.IsNullOrWhiteSpace(v.ValidationMessage))
            Console.WriteLine($"  {v.ValidationMessage}");

        if (!v.IsValid) { ConsoleUi.Pause(); return; }

        Console.WriteLine("[C] Confirm Allocation     [B] Back");
        if (ConsoleUi.ReadLine("Choice: ").ToUpperInvariant() != "C") return;

        var create = await api.CreateAllocationAsync(request, ct);
        if (!create.IsSuccess) ConsoleUi.ShowApiError(create.Error);
        else
        {
            var a = create.Data!;
            ConsoleUi.ShowSuccess($"Allocation saved. {a.EmployeeName} -> {a.ProjectName} ({a.UtilisationPercentage}%, {ConsoleUi.FormatDate(a.FromDate)}-{ConsoleUi.FormatDate(a.ToDate)})");
            ConsoleUi.Pause();
        }
    }

    private async Task EndAllocationAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("END ALLOCATION");
        var projectId = await SelectProjectAsync(ct);
        if (projectId is null) return;

        var result = await api.GetAllocationsOnProjectAsync(projectId.Value, ct);
        if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }
        var allocations = result.Data!;
        if (allocations.Count == 0) { ConsoleUi.ShowError("No active allocations on this project."); ConsoleUi.Pause(); return; }

        Console.WriteLine();
        Console.WriteLine("Active Allocations on this project:");
        Console.WriteLine($"  {"#",-3} {"Employee",-16} {"%",-6} {"From",-12} {"To"}");
        for (var i = 0; i < allocations.Count; i++)
        {
            var a = allocations[i];
            Console.WriteLine($"  {i + 1}.  {a.EmployeeName,-14} {a.UtilisationPercent,4}%  {ConsoleUi.FormatDate(a.FromDate),-12} {ConsoleUi.FormatDate(a.ToDate)}");
        }

        ConsoleUi.WriteDivider();
        var choice = ConsoleUi.ReadMenuOption("Select allocation to end: ", 1, allocations.Count);
        var selected = allocations[choice - 1];

        Console.WriteLine();
        Console.WriteLine($"End {selected.EmployeeName}'s allocation?");
        Console.WriteLine($"Set end date to today ({ConsoleUi.FormatDate(DateOnly.FromDateTime(DateTime.Today))})?");

        if (!ConsoleUi.Confirm("Confirm")) return;

        var end = await api.EndAllocationAsync(selected.AllocationId, ct);
        if (!end.IsSuccess) ConsoleUi.ShowApiError(end.Error);
        else
        {
            var e = end.Data!;
            if (e.WasRemoved)
            {
                ConsoleUi.ShowSuccess(
                    $"Future allocation cancelled. {e.EmployeeName} removed from {e.ProjectName} before it started.");
            }
            else
            {
                ConsoleUi.ShowSuccess(
                    $"Allocation ended. {e.EmployeeName} freed from {e.ProjectName} as of {ConsoleUi.FormatDate(e.EndedOnDate)}.");
            }

            Console.WriteLine($"Employee status updated to {e.EmployeeNewStatus.ToUpperInvariant()}.");
            ConsoleUi.Pause();
        }
    }
}

