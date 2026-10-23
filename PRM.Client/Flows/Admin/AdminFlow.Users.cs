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
}

