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
    // ── Projects ────────────────────────────────────────────────

    private async Task ManageProjectsAsync(CancellationToken ct)
    {
        while (true)
        {
            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("MANAGE PROJECTS");
            Console.WriteLine("1. Create Project");
            Console.WriteLine("2. View All Projects");
            Console.WriteLine("3. Update Project Details");
            Console.WriteLine("4. Manage Milestones");
            Console.WriteLine("5. Back");
            Console.WriteLine();

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 5))
            {
                case 1: await CreateProjectAsync(ct); break;
                case 2: await ViewAllProjectsAsync(ct); break;
                case 3: await UpdateProjectAsync(ct); break;
                case 4: await ManageMilestonesAsync(ct); break;
                case 5: return;
            }
        }
    }

    private async Task CreateProjectAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("CREATE PROJECT");
        var name = ConsoleUi.ReadRequired("Project Name        : ");
        var description = ConsoleUi.ReadLine("Description         : ");
        var startDate = ConsoleUi.ReadDateFromToday("Start Date");
        var endDate = ConsoleUi.ReadEndDateAfterStart(startDate, "End Date");
        if (endDate < ConsoleUi.Today)
        {
            ConsoleUi.ShowError("End date must be in the future.");
            ConsoleUi.Pause();
            return;
        }
        Console.WriteLine("Status              : (1) PLANNED   (2) ACTIVE   (3) ON_HOLD");
        var statusChoice = ConsoleUi.ReadMenuOption("Enter choice: ", 1, 3);
        var status = statusChoice switch { 1 => "Planned", 2 => "Active", _ => "OnHold" };
        var managerIdText = ConsoleUi.ReadRequired("Assign Manager (Manager ID): ");
        if (!int.TryParse(managerIdText, out var managerId)) { ConsoleUi.ShowError("Invalid manager ID."); ConsoleUi.Pause(); return; }

        ConsoleUi.WriteDivider();
        Console.WriteLine("[S] Save     [B] Back");
        if (!ConsoleUi.ReadSaveOrBack(out var save) || !save) return;

        var result = await api.CreateProjectAsync(new CreateProjectRequest(name.Trim(), description, startDate, endDate, status, managerId), ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("Project created."); ConsoleUi.Pause(); }
    }

    private async Task ViewAllProjectsAsync(CancellationToken ct)
    {
        var result = await api.GetProjectsAsync(ct);
        if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }

        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("ALL PROJECTS");
        Console.WriteLine($"{"ID",-5} {"Name",-18} {"Manager",-14} {"End Date",-12} {"Status",-10} {"Health"}");
        ConsoleUi.WriteDivider();

        foreach (var p in result.Data!.Projects)
            Console.WriteLine($"{p.Id,-5} {p.Name,-18} {p.ManagerName,-14} {ConsoleUi.FormatDate(p.EndDate),-12} {p.Status,-10} {ConsoleUi.HealthWithEmoji(p.Health)}");

        ConsoleUi.WriteDivider();
        Console.WriteLine("[B] Back");
        ConsoleUi.ReadLine("");
    }

    private async Task UpdateProjectAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("UPDATE PROJECT DETAILS");

        var id = await PromptProjectIdAsync(ct);
        if (id is null) return;

        var existing = await api.GetProjectAsync(id.Value, ct);
        if (!existing.IsSuccess) { ConsoleUi.ShowApiError(existing.Error); return; }
        var p = existing.Data!;

        Console.WriteLine();
        Console.WriteLine($"── {p.Name} ───────────────────────────────");
        var name = ConsoleUi.ReadLine($"Project Name ({p.Name}): ");
        var description = ConsoleUi.ReadLine($"Description ({p.Description}): ");
        var startInput = ConsoleUi.ReadLine($"Start Date ({ConsoleUi.FormatDate(p.StartDate)}): ");
        var endInput = ConsoleUi.ReadLine($"End Date ({ConsoleUi.FormatDate(p.EndDate)}): ");
        Console.WriteLine("Status: (1) PLANNED  (2) ACTIVE  (3) ON_HOLD  (4) COMPLETED");
        var statusInput = ConsoleUi.ReadLine("Enter choice or press Enter to keep: ");
        var managerInput = ConsoleUi.ReadLine($"Assign Manager ID ({p.ManagerId}): ");

        var status = p.Status;
        if (!string.IsNullOrWhiteSpace(statusInput) && int.TryParse(statusInput, out var sc))
            status = sc switch { 1 => "Planned", 2 => "Active", 3 => "OnHold", 4 => "Completed", _ => p.Status };

        var startDate = p.StartDate;
        if (!string.IsNullOrWhiteSpace(startInput))
        {
            if (!ConsoleUi.TryParseDateStrict(startInput, out startDate))
            {
                ConsoleUi.ShowError("Invalid start date format. Use DD-MM-YYYY.");
                ConsoleUi.Pause();
                return;
            }

            if (startDate < ConsoleUi.Today)
            {
                ConsoleUi.ShowError("Start date cannot be moved to a past date.");
                ConsoleUi.Pause();
                return;
            }
        }

        var endDate = p.EndDate;
        if (!string.IsNullOrWhiteSpace(endInput))
        {
            if (!ConsoleUi.TryParseDateStrict(endInput, out endDate))
            {
                ConsoleUi.ShowError("Invalid end date format. Use DD-MM-YYYY.");
                ConsoleUi.Pause();
                return;
            }
        }

        if (endDate <= startDate)
        {
            ConsoleUi.ShowError("End date must be after start date.");
            ConsoleUi.Pause();
            return;
        }

        var managerId = p.ManagerId;
        if (!string.IsNullOrWhiteSpace(managerInput) && int.TryParse(managerInput, out var mid)) managerId = mid;

        ConsoleUi.WriteDivider();
        Console.WriteLine("[S] Save     [B] Back");
        if (!ConsoleUi.ReadSaveOrBack(out var save) || !save) return;

        var request = new UpdateProjectRequest(
            string.IsNullOrWhiteSpace(name) ? p.Name : name,
            string.IsNullOrWhiteSpace(description) ? p.Description : description,
            startDate, endDate, status, managerId);

        var result = await api.UpdateProjectAsync(id.Value, request, ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("Project updated."); ConsoleUi.Pause(); }
    }

    private async Task ManageMilestonesAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("MILESTONES");

        var projectId = await PromptProjectIdAsync(ct);
        if (projectId is null) return;

        while (true)
        {
            var project = await api.GetProjectAsync(projectId.Value, ct);
            if (!project.IsSuccess) { ConsoleUi.ShowApiError(project.Error); return; }
            var p = project.Data!;
            var milestones = p.Milestones.ToList();

            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("MILESTONES");
            Console.WriteLine($"── {p.Name} ───────────────────────────────");
            Console.WriteLine($"{"#",-4} {"Title",-20} {"Due Date",-12} {"Status"}");
            ConsoleUi.WriteDivider();

            for (var i = 0; i < milestones.Count; i++)
                Console.WriteLine($"{i + 1}.  {milestones[i].Title,-18} {ConsoleUi.FormatDate(milestones[i].DueDate),-12} {milestones[i].Status}");

            var done = milestones.Count(m => m.Status.Equals("Done", StringComparison.OrdinalIgnoreCase));
            ConsoleUi.WriteDivider();
            Console.WriteLine($"Total: {milestones.Count} milestones   |   Completed: {done}");
            Console.WriteLine();
            Console.WriteLine("1. Add Milestone");
            Console.WriteLine("2. Update Milestone Status");
            Console.WriteLine("3. Update Milestone Due Date");
            Console.WriteLine("4. Back");

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 4))
            {
                case 1:
                    var title = ConsoleUi.ReadRequired("Milestone Title  : ");
                    var due = ConsoleUi.ReadDateRequired("Due Date (DD-MM-YYYY): ");
                    var add = await api.AddMilestoneAsync(projectId.Value, new AddMilestoneRequest(title, due), ct);
                    if (!add.IsSuccess) ConsoleUi.ShowApiError(add.Error);
                    else { ConsoleUi.ShowSuccess("Milestone added."); ConsoleUi.Pause(); }
                    break;
                case 2:
                    if (milestones.Count == 0) { ConsoleUi.ShowError("No milestones."); ConsoleUi.Pause(); break; }
                    var idx = ConsoleUi.ReadMenuOption("Enter Milestone #: ", 1, milestones.Count) - 1;
                    Console.WriteLine("New Status: (1) NOT_STARTED  (2) IN_PROGRESS  (3) DONE");
                    var st = ConsoleUi.ReadMenuOption("Enter choice: ", 1, 3);
                    var status = st switch { 1 => "NotStarted", 2 => "InProgress", _ => "Done" };
                    var upd = await api.UpdateMilestoneStatusAsync(milestones[idx].Id, new UpdateMilestoneStatusRequest(status), ct);
                    if (!upd.IsSuccess) ConsoleUi.ShowApiError(upd.Error);
                    else { ConsoleUi.ShowSuccess("Milestone updated."); ConsoleUi.Pause(); }
                    break;
                case 3:
                    if (milestones.Count == 0) { ConsoleUi.ShowError("No milestones."); ConsoleUi.Pause(); break; }
                    var dueIdx = ConsoleUi.ReadMenuOption("Enter Milestone #: ", 1, milestones.Count) - 1;
                    var selected = milestones[dueIdx];
                    Console.WriteLine($"Current due date: {ConsoleUi.FormatDate(selected.DueDate)}");
                    var newDue = ConsoleUi.ReadDateRequired("New Due Date (DD-MM-YYYY): ");
                    var dueUpd = await api.UpdateMilestoneDueDateAsync(
                        selected.Id, new UpdateMilestoneDueDateRequest(newDue), ct);
                    if (!dueUpd.IsSuccess) ConsoleUi.ShowApiError(dueUpd.Error);
                    else { ConsoleUi.ShowSuccess("Due date updated."); ConsoleUi.Pause(); }
                    break;
                case 4:
                    return;
            }
        }
    }
}

