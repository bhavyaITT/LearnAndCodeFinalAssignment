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
}

