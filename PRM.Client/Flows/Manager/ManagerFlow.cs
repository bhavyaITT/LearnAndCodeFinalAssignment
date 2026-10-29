using PRM.Application.DTOs;
using PRM.Application.DTOs.Manager;
using PRM.Client.Abstractions;
using PRM.Client.Services;
using PRM.Client.UI;

namespace PRM.Client.Flows.Manager;

public sealed partial class ManagerFlow(PrmApiClient api, UserSession session) : IRoleFlow
{
    public async Task RunAsync(CancellationToken ct)
    {
        while (true)
        {
            ConsoleUi.ClearScreen();
            ConsoleUi.WriteWelcomeHeader(session.FullName);
            Console.WriteLine();
            Console.WriteLine("1. Resource Dashboard");
            Console.WriteLine("2. Allocate Resource");
            Console.WriteLine("3. My Projects");
            Console.WriteLine("4. Timesheets");
            Console.WriteLine("5. AI Assistant");
            Console.WriteLine("6. Logout");
            Console.WriteLine();

            switch (ConsoleUi.ReadMenuOption("Enter option: ", 1, 6))
            {
                case 1: await ResourceDashboardAsync(ct); break;
                case 2: await AllocateResourceAsync(ct); break;
                case 3: await MyProjectsAsync(ct); break;
                case 4: await TeamTimesheetsAsync(ct); break;
                case 5: await AiAssistantAsync(ct); break;
                case 6: session.Logout(); return;
            }
        }
    }
}
