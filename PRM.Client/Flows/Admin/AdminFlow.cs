using PRM.Application.DTOs;
using PRM.Application.DTOs.Allocation;
using PRM.Application.DTOs.Employee;
using PRM.Application.DTOs.Project;
using PRM.Application.DTOs.Users;
using PRM.Client.Abstractions;
using PRM.Client.Services;
using PRM.Client.UI;

namespace PRM.Client.Flows.Admin;

public sealed partial class AdminFlow(PrmApiClient api, UserSession session) : IRoleFlow
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
}
