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

