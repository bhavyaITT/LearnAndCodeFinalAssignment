using Microsoft.Extensions.Configuration;
using PRM.Client.Abstractions;
using PRM.Client.Flows.Admin;
using PRM.Client.Flows.Auth;
using PRM.Client.Flows.Employee;
using PRM.Client.Flows.Manager;
using PRM.Client.Services;
using PRM.Client.Services.Api;
using PRM.Client.UI;

namespace PRM.Client.Composition;

/// <summary>Application entry point — wires dependencies and runs the main loop.</summary>
public sealed class AppHost
{
    private readonly UserSession _session = new();
    private readonly PrmApiClient _api;
    private readonly AuthFlow _authFlow;
    private readonly IReadOnlyDictionary<string, IRoleFlow> _roleFlows;

    public AppHost(IConfiguration configuration)
    {
        var baseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7295";

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = static (_, _, _, _) => true
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        _api = new PrmApiClient(http, _session);
        _authFlow = new AuthFlow(_api, _session);

        var adminFlow = new AdminFlow(_api, _session);
        var managerFlow = new ManagerFlow(_api, _session);
        var employeeFlow = new EmployeeFlow(_api, _session);

        _roleFlows = new Dictionary<string, IRoleFlow>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] = adminFlow,
            ["MANAGER"] = managerFlow,
            ["EMPLOYEE"] = employeeFlow
        };
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        while (true)
        {
            if (!await _authFlow.RunStartupAsync(ct))
            {
                ConsoleUi.ClearScreen();
                Console.WriteLine("Goodbye.");
                break;
            }

            if (!_roleFlows.TryGetValue(_session.Role, out var flow))
            {
                ConsoleUi.ShowError($"Unknown role: {_session.Role}");
                _session.Logout();
                continue;
            }

            await flow.RunAsync(ct);
        }
    }
}
