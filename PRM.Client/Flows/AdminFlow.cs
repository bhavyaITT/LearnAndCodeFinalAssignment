using PRM.Application.DTOs;
using PRM.Application.DTOs.Allocation;
using PRM.Application.DTOs.Employee;
using PRM.Application.DTOs.Project;
using PRM.Application.DTOs.Users;
using PRM.Client.Services;
using PRM.Client.UI;

namespace PRM.Client.Flows;

public sealed class AdminFlow(PrmApiClient api, UserSession session)
{
    public async Task RunAsync(CancellationToken ct)
    {
        while (true)
        {
            ConsoleUi.ClearScreen();
            ConsoleUi.WriteWelcomeHeader(session.FullName);
            Console.WriteLine("ADMIN PANEL");
            Console.WriteLine();
            Console.WriteLine("1. Manage Employees");
            Console.WriteLine("2. Manage Projects");
            Console.WriteLine("3. View All Allocations");
            Console.WriteLine("4. Manage Users");
            Console.WriteLine("5. System Configuration");
            Console.WriteLine("6. Logout");
            Console.WriteLine();

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 6))
            {
                case 1: await ManageEmployeesAsync(ct); break;
                case 2: await ManageProjectsAsync(ct); break;
                case 3: await ViewAllocationsAsync(ct); break;
                case 4: await ManageUsersAsync(ct); break;
                case 5: await SystemConfigurationAsync(ct); break;
                case 6: session.Logout(); return;
            }
        }
    }

    // ── Employees ───────────────────────────────────────────────

    private async Task ManageEmployeesAsync(CancellationToken ct)
    {
        while (true)
        {
            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("MANAGE EMPLOYEES");
            Console.WriteLine("1. View All Employees");
            Console.WriteLine("2. Update Employee");
            Console.WriteLine("3. Deactivate Employee");
            Console.WriteLine("4. Manage Employee Skills");
            Console.WriteLine("5. Assign Manager");
            Console.WriteLine("6. Back");
            Console.WriteLine();

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 6))
            {
                case 1: await ViewAllEmployeesAsync(ct); break;
                case 2: await UpdateEmployeeAsync(ct); break;
                case 3: await DeactivateEmployeeAsync(ct); break;
                case 4: await ManageSkillsAsync(ct); break;
                case 5: await AssignManagerAsync(ct); break;
                case 6: return;
            }
        }
    }

    private async Task ViewAllEmployeesAsync(CancellationToken ct)
    {
        var result = await api.GetEmployeesAsync(ct);
        if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }

        var data = result.Data!;
        var employees = data.Employees.ToList();
        string? statusFilter = null;
        string? deptFilter = null;

        while (true)
        {
            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("ALL EMPLOYEES");
            Console.WriteLine($"{"ID",-5} {"Name",-16} {"Role",-10} {"Department",-12} {"Status"}");
            ConsoleUi.WriteDivider();

            var filtered = employees.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(statusFilter))
                filtered = filtered.Where(e => e.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(deptFilter))
                filtered = filtered.Where(e => e.Department.Equals(deptFilter, StringComparison.OrdinalIgnoreCase));

            var list = filtered.ToList();
            foreach (var e in list)
                Console.WriteLine($"{e.Id,-5} {e.FullName,-16} {e.Role,-10} {e.Department,-12} {e.Status.ToUpperInvariant()}");

            ConsoleUi.WriteDivider();
            Console.WriteLine($"Total: {list.Count}   |   Allocated: {list.Count(e => e.Status.Equals("Allocated", StringComparison.OrdinalIgnoreCase))}   |   Bench: {list.Count(e => e.Status.Equals("Bench", StringComparison.OrdinalIgnoreCase))}");
            if (!string.IsNullOrWhiteSpace(statusFilter) || !string.IsNullOrWhiteSpace(deptFilter))
            {
                var filters = new List<string>();
                if (!string.IsNullOrWhiteSpace(statusFilter)) filters.Add($"Status={statusFilter}");
                if (!string.IsNullOrWhiteSpace(deptFilter)) filters.Add($"Department={deptFilter}");
                Console.WriteLine($"Filters: {string.Join(", ", filters)}");
            }
            Console.WriteLine();
            Console.WriteLine("[F] Filter by Status / Department     [B] Back");

            var action = ConsoleUi.ReadLine("Choice: ").ToUpperInvariant();
            if (action == "B") return;
            if (action == "F")
            {
                statusFilter = PickStatusFilter(statusFilter);
                deptFilter = PickDepartmentFilter(deptFilter);
            }
        }
    }

    private static string? PickStatusFilter(string? current)
    {
        Console.WriteLine();
        Console.WriteLine("Filter by Status:");
        Console.WriteLine("  (1) All");
        Console.WriteLine("  (2) Bench");
        Console.WriteLine("  (3) Allocated");
        if (!string.IsNullOrWhiteSpace(current))
            Console.WriteLine($"  Current: {current}");
        var choice = ConsoleUi.ReadLine("Enter choice: ").Trim();
        return choice switch
        {
            "1" or "" => null,
            "2" => "Bench",
            "3" => "Allocated",
            _ => current
        };
    }

    private static string? PickDepartmentFilter(string? current)
    {
        Console.WriteLine();
        Console.WriteLine("Filter by Department:");
        Console.WriteLine("  (1) All");
        Console.WriteLine("  (2) Development");
        Console.WriteLine("  (3) HR");
        Console.WriteLine("  (4) Management");
        Console.WriteLine("  (5) Administration");
        Console.WriteLine("  (6) Testing");
        Console.WriteLine("  (7) SDET");
        if (!string.IsNullOrWhiteSpace(current))
            Console.WriteLine($"  Current: {current}");
        var choice = ConsoleUi.ReadLine("Enter choice: ").Trim();
        return choice switch
        {
            "1" or "" => null,
            "2" => "Development",
            "3" => "HR",
            "4" => "Management",
            "5" => "Administration",
            "6" => "Testing",
            "7" => "SDET",
            _ => current
        };
    }

    private static string PickDepartmentRequired()
    {
        Console.WriteLine("Department:");
        Console.WriteLine("  (1) Development  (2) HR  (3) Management");
        Console.WriteLine("  (4) Administration  (5) Testing  (6) SDET");
        var choice = ConsoleUi.ReadMenuOption("Enter choice: ", 1, 6);
        return choice switch
        {
            1 => "Development",
            2 => "HR",
            3 => "Management",
            4 => "Administration",
            5 => "Testing",
            _ => "SDET"
        };
    }

    private static string PickDesignationRequired()
    {
        Console.WriteLine("Designation:");
        Console.WriteLine("  (1) SE  (2) Manager  (3) Admin");
        var choice = ConsoleUi.ReadMenuOption("Enter choice: ", 1, 3);
        return choice switch { 1 => "SE", 2 => "Manager", _ => "Admin" };
    }

    private static string? PickDepartment(string? current)
    {
        Console.WriteLine($"Department ({current}):");
        Console.WriteLine("  (1) Development  (2) HR  (3) Management");
        Console.WriteLine("  (4) Administration  (5) Testing  (6) SDET  (Enter) keep current");

        var choice = ConsoleUi.ReadOptionalMenuChoice("Enter choice: ", 1, 6);
        return choice switch
        {
            null => null,
            1 => "Development",
            2 => "HR",
            3 => "Management",
            4 => "Administration",
            5 => "Testing",
            6 => "SDET",
            _ => null
        };
    }

    private static string? PickDesignation(string? current)
    {
        Console.WriteLine($"Designation ({current}):");
        Console.WriteLine("  (1) SE  (2) Manager  (3) Admin  (Enter) keep current");

        var choice = ConsoleUi.ReadOptionalMenuChoice("Enter choice: ", 1, 3);
        return choice switch
        {
            null => null,
            1 => "SE",
            2 => "Manager",
            3 => "Admin",
            _ => null
        };
    }

    private static void PrintManageableEmployeesTable(IEnumerable<EmployeeSummaryResponse> employees)
    {
        Console.WriteLine($"{"ID",-5} {"Name",-16} {"Role",-10} {"Department",-12} {"Status"}");
        ConsoleUi.WriteDivider();

        foreach (var e in employees.OrderBy(e => e.Id))
            Console.WriteLine($"{e.Id,-5} {e.FullName,-16} {e.Role,-10} {e.Department,-12} {e.Status.ToUpperInvariant()}");

        ConsoleUi.WriteDivider();
    }

    private async Task<int?> PromptManageableEmployeeIdAsync(CancellationToken ct, string idPrompt = "Enter Employee ID: ")
    {
        var result = await api.GetEmployeesAsync(ct);
        if (!result.IsSuccess)
        {
            ConsoleUi.ShowApiError(result.Error);
            return null;
        }

        var manageable = result.Data!.Employees
            .Where(e => IsManageableRole(e.Role))
            .ToList();

        if (manageable.Count == 0)
        {
            ConsoleUi.ShowError("No employees or managers found.");
            ConsoleUi.Pause();
            return null;
        }

        PrintManageableEmployeesTable(manageable);
        Console.WriteLine();

        var idText = ConsoleUi.ReadRequired(idPrompt);
        if (!int.TryParse(idText, out var id))
        {
            ConsoleUi.ShowError("Invalid ID.");
            ConsoleUi.Pause();
            return null;
        }

        if (manageable.All(e => e.Id != id))
        {
            ConsoleUi.ShowError("Employee ID not found in the list above.");
            ConsoleUi.Pause();
            return null;
        }

        return id;
    }

    private async Task<int?> PromptProjectIdAsync(CancellationToken ct, string idPrompt = "Enter Project ID: ")
    {
        var result = await api.GetProjectsAsync(ct);
        if (!result.IsSuccess)
        {
            ConsoleUi.ShowApiError(result.Error);
            return null;
        }

        var projects = result.Data!.Projects.OrderBy(p => p.Id).ToList();
        if (projects.Count == 0)
        {
            ConsoleUi.ShowError("No projects found.");
            ConsoleUi.Pause();
            return null;
        }

        Console.WriteLine($"{"ID",-5} {"Name",-18} {"Manager",-14} {"End Date",-12} {"Status",-10} {"Health"}");
        ConsoleUi.WriteDivider();

        foreach (var p in projects)
            Console.WriteLine($"{p.Id,-5} {p.Name,-18} {p.ManagerName,-14} {ConsoleUi.FormatDate(p.EndDate),-12} {p.Status,-10} {ConsoleUi.HealthWithEmoji(p.Health)}");

        ConsoleUi.WriteDivider();
        Console.WriteLine();

        var idText = ConsoleUi.ReadRequired(idPrompt);
        if (!int.TryParse(idText, out var id))
        {
            ConsoleUi.ShowError("Invalid ID.");
            ConsoleUi.Pause();
            return null;
        }

        if (projects.All(p => p.Id != id))
        {
            ConsoleUi.ShowError("Project ID not found in the list above.");
            ConsoleUi.Pause();
            return null;
        }

        return id;
    }

    private static bool IsManageableRole(string role) =>
        role.Equals("Employee", StringComparison.OrdinalIgnoreCase)
        || role.Equals("Manager", StringComparison.OrdinalIgnoreCase);

    private async Task UpdateEmployeeAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("UPDATE EMPLOYEE");

        var id = await PromptManageableEmployeeIdAsync(ct);
        if (id is null) return;

        var existing = await api.GetEmployeeAsync(id.Value, ct);
        if (!existing.IsSuccess) { ConsoleUi.ShowApiError(existing.Error); return; }

        var emp = existing.Data!;
        if (!IsManageableRole(emp.Role))
        {
            ConsoleUi.ShowError("Only users with Employee or Manager roles can be updated here.");
            ConsoleUi.Pause();
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"── {emp.FullName} ({emp.Role}) ─────────────────────────────");
        var fullName = ConsoleUi.ReadLine($"Full Name ({emp.FullName}): ");
        var email = ConsoleUi.ReadIntimetecEmail($"Email ({emp.Email}): ", emp.Email);
        var departmentPick = PickDepartment(emp.Department);
        var designationPick = PickDesignation(emp.Designation);
        ConsoleUi.WriteDivider();
        Console.WriteLine("[S] Save     [B] Back");

        if (!ConsoleUi.ReadSaveOrBack(out var save) || !save) return;

        var request = new UpdateEmployeeRequest(
            string.IsNullOrWhiteSpace(fullName) ? emp.FullName : fullName.Trim(),
            email,
            departmentPick ?? emp.Department,
            designationPick ?? emp.Designation);

        var result = await api.UpdateEmployeeAsync(id.Value, request, ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("Employee updated."); ConsoleUi.Pause(); }
    }

    private async Task DeactivateEmployeeAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("DEACTIVATE EMPLOYEE");

        var id = await PromptManageableEmployeeIdAsync(ct);
        if (id is null) return;

        var empResult = await api.GetEmployeeAsync(id.Value, ct);
        if (!empResult.IsSuccess) { ConsoleUi.ShowApiError(empResult.Error); return; }
        var emp = empResult.Data!;

        if (!IsManageableRole(emp.Role))
        {
            ConsoleUi.ShowError("Only users with Employee or Manager roles can be deactivated here.");
            ConsoleUi.Pause();
            return;
        }

        var isBench = emp.Status.Equals("Bench", StringComparison.OrdinalIgnoreCase);
        List<AllocationResponse> activeAllocations = [];

        if (!isBench)
        {
            var allocResult = await api.GetAllocationsAsync(id, activeOnly: true, ct: ct);
            if (allocResult.IsSuccess)
                activeAllocations = allocResult.Data!.Allocations.ToList();
        }

        Console.WriteLine();
        Console.WriteLine($"── {emp.FullName} ({emp.Role}) ─────────────────────────────");
        Console.WriteLine($"Department : {emp.Department}");
        Console.WriteLine($"Status     : {emp.Status.ToUpperInvariant()}");
        Console.WriteLine();

        if (!isBench && activeAllocations.Count > 0)
        {
            Console.WriteLine($"Warning: This employee has {activeAllocations.Count} active allocation(s).");
            Console.WriteLine("Deactivating will remove them from:");
            foreach (var a in activeAllocations)
                Console.WriteLine($"  - {a.ProjectName}  ({a.UtilisationPercentage}%,  ends {ConsoleUi.FormatDate(a.ToDate)})");
            Console.WriteLine();
            Console.WriteLine($"Are you sure you want to deactivate {emp.FullName}?");
            Console.WriteLine("This will end all active allocations today and block their login account.");
        }
        else
        {
            Console.WriteLine($"Are you sure you want to deactivate {emp.FullName}?");
            Console.WriteLine("This will block their login account.");
        }

        Console.WriteLine("[Y] Yes, Deactivate     [B] Cancel");
        if (!ConsoleUi.ReadYesOrBack(out var confirm) || !confirm) return;

        var result = await api.DeactivateEmployeeAsync(id.Value, ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("Employee deactivated."); ConsoleUi.Pause(); }
    }

    private async Task ManageSkillsAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("MANAGE SKILLS");
        var idText = ConsoleUi.ReadRequired("Enter Employee ID: ");
        if (!int.TryParse(idText, out var employeeId)) { ConsoleUi.ShowError("Invalid ID."); ConsoleUi.Pause(); return; }

        var empResult = await api.GetEmployeeAsync(employeeId, ct);
        if (!empResult.IsSuccess) { ConsoleUi.ShowApiError(empResult.Error); return; }

        while (true)
        {
            var skillsResult = await api.GetEmployeeSkillsAsync(employeeId, ct);
            if (!skillsResult.IsSuccess) { ConsoleUi.ShowApiError(skillsResult.Error); return; }
            var skills = skillsResult.Data!;

            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("MANAGE SKILLS");
            Console.WriteLine($"── {empResult.Data!.FullName} ─────────────────────────────────");
            Console.WriteLine("Current Skills:");
            for (var i = 0; i < skills.Count; i++)
                Console.WriteLine($"  {i + 1}.  {skills[i].SkillName,-18} {skills[i].Proficiency}");
            ConsoleUi.WriteDivider();
            Console.WriteLine("1. Add Skill");
            Console.WriteLine("2. Update Proficiency Level");
            Console.WriteLine("3. Remove Skill");
            Console.WriteLine("4. Back");
            Console.WriteLine();

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 4))
            {
                case 1:
                    await AddSkillAsync(employeeId, ct);
                    break;
                case 2:
                    if (skills.Count == 0) { ConsoleUi.ShowError("No skills to update."); ConsoleUi.Pause(); break; }
                    var updIdx = ConsoleUi.ReadMenuOption("Enter skill #: ", 1, skills.Count) - 1;
                    await UpdateProficiencyAsync(skills[updIdx].Id, ct);
                    break;
                case 3:
                    if (skills.Count == 0) { ConsoleUi.ShowError("No skills to remove."); ConsoleUi.Pause(); break; }
                    var remIdx = ConsoleUi.ReadMenuOption("Enter skill #: ", 1, skills.Count) - 1;
                    Console.WriteLine();
                    if (ConsoleUi.ReadYesOrNo($"Remove {skills[remIdx].SkillName}?"))
                    {
                        var rem = await api.RemoveSkillAsync(skills[remIdx].Id, ct);
                        if (!rem.IsSuccess) ConsoleUi.ShowApiError(rem.Error);
                        else { ConsoleUi.ShowSuccess("Skill removed."); ConsoleUi.Pause(); }
                    }
                    break;
                case 4:
                    return;
            }
        }
    }

    private async Task AddSkillAsync(int employeeId, CancellationToken ct)
    {
        Console.WriteLine();
        var name = ConsoleUi.ReadRequired("Skill Name        : ");
        Console.WriteLine("Category          : (1) Backend  (2) Frontend  (3) DevOps  (4) QA  (5) Other");
        var catChoice = ConsoleUi.ReadMenuOption("Enter choice      : ", 1, 5);
        var category = catChoice switch { 1 => "Backend", 2 => "Frontend", 3 => "DevOps", 4 => "QA", _ => "Other" };
        Console.WriteLine("Proficiency Level : (1) Beginner  (2) Intermediate  (3) Advanced");
        var profChoice = ConsoleUi.ReadMenuOption("Enter choice      : ", 1, 3);
        var proficiency = profChoice switch { 1 => "Beginner", 2 => "Intermediate", _ => "Advanced" };

        var result = await api.AddSkillAsync(employeeId, new AddSkillRequest(name, category, proficiency), ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("Skill added."); ConsoleUi.Pause(); }
    }

    private async Task UpdateProficiencyAsync(int skillId, CancellationToken ct)
    {
        Console.WriteLine("Proficiency Level : (1) Beginner  (2) Intermediate  (3) Advanced");
        var profChoice = ConsoleUi.ReadMenuOption("Enter choice: ", 1, 3);
        var proficiency = profChoice switch { 1 => "Beginner", 2 => "Intermediate", _ => "Advanced" };
        var result = await api.UpdateSkillProficiencyAsync(skillId, new UpdateSkillProficiencyRequest(proficiency), ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("Proficiency updated."); ConsoleUi.Pause(); }
    }

    private async Task AssignManagerAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("ASSIGN MANAGER");

        var employeesResult = await api.GetEmployeesAsync(ct);
        if (!employeesResult.IsSuccess) { ConsoleUi.ShowApiError(employeesResult.Error); return; }

        var all = employeesResult.Data!.Employees.ToList();
        var eligibleEmployees = all
            .Where(e => e.Role.Equals("Employee", StringComparison.OrdinalIgnoreCase) && e.IsActive)
            .OrderBy(e => e.Id)
            .ToList();

        if (eligibleEmployees.Count == 0)
        {
            ConsoleUi.ShowError("No active employees available for manager assignment.");
            ConsoleUi.Pause();
            return;
        }

        Console.WriteLine("Active Employees (Employee role only):");
        Console.WriteLine($"{"ID",-5} {"Name",-20} {"Department",-14} {"Status"}");
        ConsoleUi.WriteDivider();

        foreach (var e in eligibleEmployees)
            Console.WriteLine($"{e.Id,-5} {e.FullName,-20} {e.Department,-14} {e.Status.ToUpperInvariant()}");

        ConsoleUi.WriteDivider();
        Console.WriteLine();

        var empIdText = ConsoleUi.ReadRequired("Employee ID : ");
        if (!int.TryParse(empIdText, out var empId))
        {
            ConsoleUi.ShowError("Invalid ID.");
            ConsoleUi.Pause();
            return;
        }

        var employee = eligibleEmployees.FirstOrDefault(e => e.Id == empId);
        if (employee is null)
        {
            ConsoleUi.ShowError("Only active employees with the Employee role can be assigned a manager.");
            ConsoleUi.Pause();
            return;
        }

        var managers = all
            .Where(e => e.Role.Equals("Manager", StringComparison.OrdinalIgnoreCase) && e.IsActive)
            .OrderBy(e => e.Id)
            .ToList();

        if (managers.Count == 0)
        {
            ConsoleUi.ShowError("No active managers found.");
            ConsoleUi.Pause();
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Active Managers:");
        Console.WriteLine($"{"ID",-5} {"Name",-20} {"Department"}");
        ConsoleUi.WriteDivider();

        foreach (var m in managers)
            Console.WriteLine($"{m.Id,-5} {m.FullName,-20} {m.Department}");

        ConsoleUi.WriteDivider();
        Console.WriteLine();

        var mgrIdText = ConsoleUi.ReadRequired("Manager ID  : ");
        if (!int.TryParse(mgrIdText, out var mgrId))
        {
            ConsoleUi.ShowError("Invalid ID.");
            ConsoleUi.Pause();
            return;
        }

        var manager = managers.FirstOrDefault(e => e.Id == mgrId);
        if (manager is null)
        {
            ConsoleUi.ShowError("Manager not found or inactive.");
            ConsoleUi.Pause();
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Assign {manager.FullName} as manager for {employee.FullName}.");
        ConsoleUi.WriteDivider();
        Console.WriteLine("[S] Save     [B] Back");
        if (!ConsoleUi.ReadSaveOrBack(out var save) || !save) return;

        var result = await api.AssignManagerAsync(empId, mgrId, ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("Manager assigned successfully."); ConsoleUi.Pause(); }
    }

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

    // ── Users ───────────────────────────────────────────────────

    private async Task ManageUsersAsync(CancellationToken ct)
    {
        while (true)
        {
            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("MANAGE USERS");
            Console.WriteLine("1. Create User Account");
            Console.WriteLine("2. View All Users");
            Console.WriteLine("3. Reset User Password");
            Console.WriteLine("4. Deactivate User");
            Console.WriteLine("5. Back");
            Console.WriteLine();

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 5))
            {
                case 1: await CreateUserAsync(ct); break;
                case 2: await ViewAllUsersAsync(ct); break;
                case 3: await ResetPasswordAsync(ct); break;
                case 4: await DeactivateUserAsync(ct); break;
                case 5: return;
            }
        }
    }

    private async Task CreateUserAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("CREATE USER ACCOUNT");
        var fullName = ConsoleUi.ReadRequired("Full Name         : ");
        var email = ConsoleUi.ReadIntimetecEmail("Email             : ");
        var username = ConsoleUi.ReadRequired("Username          : ");
        var tempPassword = ConsoleUi.ReadPassword("Temporary Password: ");
        Console.WriteLine("Role              : (1) Admin  (2) Manager  (3) Employee");
        var roleChoice = ConsoleUi.ReadMenuOption("Enter choice: ", 1, 3);
        var role = roleChoice switch { 1 => "Admin", 2 => "Manager", _ => "Employee" };
        var department = PickDepartmentRequired();
        var designation = PickDesignationRequired();

        ConsoleUi.WriteDivider();
        Console.WriteLine("[S] Save     [B] Back");
        if (!ConsoleUi.ReadSaveOrBack(out var save) || !save) return;

        var result = await api.CreateUserAsync(new CreateUserRequest(fullName, email, username, tempPassword, role, department, designation), ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else
        {
            ConsoleUi.ShowSuccess("Account created. User must change password on first login.");
            ConsoleUi.Pause();
        }
    }

    private async Task ViewAllUsersAsync(CancellationToken ct)
    {
        while (true)
        {
            var result = await api.GetUsersAsync(ct);
            if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }

            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("ALL USERS");
            Console.WriteLine($"{"ID",-5} {"Username",-18} {"Role",-10} {"Status"}");
            ConsoleUi.WriteDivider();

            foreach (var u in result.Data!.Users)
                Console.WriteLine($"{u.Id,-5} {u.Username,-18} {u.Role.ToUpperInvariant(),-10} {(u.IsActive ? "Active" : "Inactive")}");

            ConsoleUi.WriteDivider();
            Console.WriteLine($"Total: {result.Data.TotalCount}   |   Active: {result.Data.ActiveCount}   |   Inactive: {result.Data.InactiveCount}");
            Console.WriteLine("[R] Reactivate a user     [B] Back");

            var action = ConsoleUi.ReadLine("Choice: ").ToUpperInvariant();
            if (action == "B") return;
            if (action == "R")
                await ReactivateUserAsync(ct);
        }
    }

    private async Task ReactivateUserAsync(CancellationToken ct)
    {
        var idText = ConsoleUi.ReadRequired("Enter User ID to reactivate: ");
        var users = await api.GetUsersAsync(ct);
        if (!users.IsSuccess) { ConsoleUi.ShowApiError(users.Error); return; }

        var user = users.Data!.Users.FirstOrDefault(u => u.Id.ToString() == idText);
        if (user is null) { ConsoleUi.ShowError("User not found."); ConsoleUi.Pause(); return; }

        Console.WriteLine();
        Console.WriteLine($"User: {user.FullName} ({user.Role}) — currently {(user.IsActive ? "Active" : "Inactive")}");
        if (!ConsoleUi.Confirm("Reactivate this account?")) return;

        var result = await api.ReactivateUserAsync(user.Username, ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else
        {
            ConsoleUi.ShowSuccess($"Account reactivated. {user.FullName} can now log in.");
            Console.WriteLine("Note: Previous allocations are NOT restored. Admin must re-allocate manually if needed.");
            ConsoleUi.Pause();
        }
    }

    private async Task ResetPasswordAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("RESET USER PASSWORD");
        var identifier = ConsoleUi.ReadRequired("Enter Username or User ID: ");

        var users = await api.GetUsersAsync(ct);
        if (!users.IsSuccess) { ConsoleUi.ShowApiError(users.Error); return; }

        var user = users.Data!.Users.FirstOrDefault(u =>
            u.Username.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            u.Id.ToString() == identifier);

        if (user is null) { ConsoleUi.ShowError("User not found."); ConsoleUi.Pause(); return; }

        Console.WriteLine();
        Console.WriteLine($"User found: {user.FullName} ({user.Role})");
        var newPassword = ConsoleUi.ReadPassword("New Temporary Password: ");
        ConsoleUi.WriteDivider();
        Console.WriteLine("[S] Save     [B] Back");
        if (ConsoleUi.ReadLine("Choice: ").ToUpperInvariant() != "S") return;

        var result = await api.ResetPasswordAsync(user.Username, new ResetPasswordRequest(newPassword), ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else
        {
            ConsoleUi.ShowSuccess("Password reset. User will be prompted to change it on next login.");
            ConsoleUi.Pause();
        }
    }

    private async Task DeactivateUserAsync(CancellationToken ct)
    {
        ConsoleUi.ClearScreen();
        ConsoleUi.WriteHeader("DEACTIVATE USER");
        var identifier = ConsoleUi.ReadRequired("Enter Username or User ID: ");

        var users = await api.GetUsersAsync(ct);
        if (!users.IsSuccess) { ConsoleUi.ShowApiError(users.Error); return; }

        var user = users.Data!.Users.FirstOrDefault(u =>
            u.Username.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            u.Id.ToString() == identifier);

        if (user is null) { ConsoleUi.ShowError("User not found."); ConsoleUi.Pause(); return; }

        Console.WriteLine();
        Console.WriteLine($"User found: {user.FullName} ({user.Role})");
        Console.WriteLine($"Status     : {(user.IsActive ? "Active" : "Inactive")}");
        Console.WriteLine();
        Console.WriteLine("Are you sure you want to deactivate this account?");
        Console.WriteLine("Deactivated users cannot log in. Their data is preserved.");

        if (!ConsoleUi.Confirm("Confirm deactivation")) return;

        var result = await api.DeactivateUserAsync(user.Username, ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("User deactivated."); ConsoleUi.Pause(); }
    }

    // ── System Config ───────────────────────────────────────────

    private async Task SystemConfigurationAsync(CancellationToken ct)
    {
        while (true)
        {
            var result = await api.GetSystemConfigAsync(ct);
            if (!result.IsSuccess) { ConsoleUi.ShowApiError(result.Error); return; }
            var config = result.Data!;

            ConsoleUi.ClearScreen();
            ConsoleUi.WriteHeader("SYSTEM CONFIGURATION");
            Console.WriteLine("Current Settings:");
            Console.WriteLine($"  LLM Provider        :  {config.LlmProvider}");
            Console.WriteLine($"  LLM API Key         :  {(config.HasApiKey ? "****************************" : "(not set)")}");
            Console.WriteLine($"  Scheduler Interval  :  {config.SchedulerIntervalHours} hours");
            Console.WriteLine($"  Max Weekly Hours    :  {config.MaxWeeklyHours}");
            ConsoleUi.WriteDivider();
            Console.WriteLine("1. Update LLM API Key");
            Console.WriteLine("2. Change LLM Provider  (Gemini / Groq / InHouse)");
            Console.WriteLine("3. Update Scheduler Interval");
            Console.WriteLine("4. Update Max Weekly Hours");
            Console.WriteLine("5. Back");

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 5))
            {
                case 1:
                    var key = ConsoleUi.ReadRequired("New LLM API Key: ");
                    await PatchConfigAsync(new UpdateSystemConfigRequest(null, key, null, null), ct);
                    break;
                case 2:
                    Console.WriteLine("(1) Gemini  (2) Groq  (3) InHouse");
                    var provChoice = ConsoleUi.ReadMenuOption("Enter choice: ", 1, 3);
                    var prov = provChoice switch { 1 => "Gemini", 2 => "Groq", _ => "InHouse" };
                    await PatchConfigAsync(new UpdateSystemConfigRequest(prov, null, null, null), ct);
                    break;
                case 3:
                    var hours = ConsoleUi.ReadIntInRange("Scheduler interval (hours): ", 1, 24);
                    await PatchConfigAsync(new UpdateSystemConfigRequest(null, null, hours, null), ct);
                    break;
                case 4:
                    var maxHrs = ConsoleUi.ReadIntInRange("Max weekly hours: ", 20, 80);
                    await PatchConfigAsync(new UpdateSystemConfigRequest(null, null, null, maxHrs), ct);
                    break;
                case 5:
                    return;
            }
        }
    }

    private async Task PatchConfigAsync(UpdateSystemConfigRequest request, CancellationToken ct)
    {
        var result = await api.UpdateSystemConfigAsync(request, ct);
        if (!result.IsSuccess) ConsoleUi.ShowApiError(result.Error);
        else { ConsoleUi.ShowSuccess("Settings updated."); ConsoleUi.Pause(); }
    }
}
