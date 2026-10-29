using PRM.Application.DTOs;
using PRM.Application.Interfaces.Service;
using System.Text.Json;

namespace PRM.Application.UseCases.AI;

/// <summary>
/// Staffs a project team from a plain-English prompt.
/// Bench employees are suggested as matches; allocated employees with matching skills
/// are surfaced as availability gaps with allocation date ranges.
/// </summary>
public class TeamSkillMatchUseCase(IQueryService queryService, ILlmClientFactory llmFactory)
{
    public async Task<Result<TeamStaffingResponse>> ExecuteAsync(
        TeamStaffingRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RequirementText))
            return Result<TeamStaffingResponse>.Failure("Requirement text cannot be empty.");

        var benchEmployees = (await queryService.GetBenchEmployeesForTeamStaffingAsync(ct)).ToList();
        var allocatedEmployees = (await queryService.GetAllocatedEmployeesForTeamStaffingAsync(ct)).ToList();

        if (benchEmployees.Count == 0 && allocatedEmployees.Count == 0)
            return Result<TeamStaffingResponse>.Failure(
                "No active employees found organisation-wide for team staffing.");

        ILlmClient llmClient;
        try
        {
            llmClient = await llmFactory.GetClientAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            return Result<TeamStaffingResponse>.Failure($"AI not available: {ex.Message}");
        }

        string rawResponse;
        try
        {
            rawResponse = await llmClient.CompleteAsync(
                TeamSkillMatchPromptBuilder.BuildSystemPrompt(),
                TeamSkillMatchPromptBuilder.BuildUserPrompt(
                    request.RequirementText,
                    request.ProjectContext,
                    benchEmployees,
                    allocatedEmployees),
                ct);
        }
        catch (Exception ex)
        {
            return Result<TeamStaffingResponse>.Failure($"AI call failed: {ex.Message}");
        }

        var parsed = ParseAiResponse(rawResponse);
        if (parsed is null)
            return Result<TeamStaffingResponse>.Failure(
                "AI returned an unreadable response. Please try again.");

        var benchById = benchEmployees.ToDictionary(e => e.EmployeeId);
        var allocatedById = allocatedEmployees.ToDictionary(e => e.EmployeeId);
        var usedEmployeeIds = new HashSet<int>();
        var matches = new List<TeamRoleMatchResponse>();
        var gaps = new List<TeamRoleGapResponse>();
        var allocatedAlternatives = new List<TeamAllocatedAlternativeResponse>();
        var coveredRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in parsed.Matches)
        {
            if (string.IsNullOrWhiteSpace(match.RoleTitle))
                continue;

            coveredRoles.Add(match.RoleTitle);

            if (benchById.TryGetValue(match.EmployeeId, out var emp)
                && usedEmployeeIds.Add(match.EmployeeId))
            {
                matches.Add(new TeamRoleMatchResponse(
                    match.RoleTitle.Trim(), emp.EmployeeId, emp.FullName, match.Reason));
            }
            else
            {
                gaps.Add(new TeamRoleGapResponse(
                    match.RoleTitle.Trim(),
                    "SkillGap",
                    benchById.ContainsKey(match.EmployeeId)
                        ? "Employee already assigned to another role in this team."
                        : "AI suggested an invalid or non-bench employee for this role.",
                    null,
                    null));
            }
        }

        foreach (var gap in parsed.Gaps)
        {
            if (string.IsNullOrWhiteSpace(gap.RoleTitle)
                || !coveredRoles.Add(gap.RoleTitle))
                continue;

            var gapType = string.IsNullOrWhiteSpace(gap.GapType) ? "SkillGap" : gap.GapType.Trim();
            var enriched = EnrichAvailabilityGap(gap, gapType, allocatedById);

            gaps.Add(enriched.Gap);

            if (enriched.Alternative is not null)
                allocatedAlternatives.Add(enriched.Alternative);
        }

        return Result<TeamStaffingResponse>.Success(new TeamStaffingResponse(
            request.RequirementText.Trim(),
            request.ProjectContext,
            matches,
            gaps,
            allocatedAlternatives,
            benchEmployees.Count,
            allocatedEmployees.Count,
            "Bench matches can be allocated now. Allocated alternatives show skilled staff already booked — plan around their dates."));
    }

    private static (TeamRoleGapResponse Gap, TeamAllocatedAlternativeResponse? Alternative) EnrichAvailabilityGap(
        AiGap gap,
        string gapType,
        Dictionary<int, AllocatedEmployeeAiContext> allocatedById)
    {
        var roleTitle = gap.RoleTitle.Trim();
        var isAvailabilityGap = gapType.Equals("AvailabilityGap", StringComparison.OrdinalIgnoreCase);

        if (isAvailabilityGap
            && gap.AllocatedEmployeeId.HasValue
            && allocatedById.TryGetValue(gap.AllocatedEmployeeId.Value, out var allocated))
        {
            var primaryAllocation = allocated.ActiveAllocations
                .OrderByDescending(a => a.ToDate)
                .FirstOrDefault();

            if (primaryAllocation is not null)
            {
                var reason = string.IsNullOrWhiteSpace(gap.Reason)
                    ? $"{allocated.FullName} has the required skills but is already allocated on " +
                      $"{primaryAllocation.ProjectName} from {primaryAllocation.FromDate:yyyy-MM-dd} " +
                      $"to {primaryAllocation.ToDate:yyyy-MM-dd}. Plan accordingly."
                    : gap.Reason;

                var alternative = new TeamAllocatedAlternativeResponse(
                    roleTitle,
                    allocated.EmployeeId,
                    allocated.FullName,
                    primaryAllocation.ProjectName,
                    primaryAllocation.FromDate,
                    primaryAllocation.ToDate,
                    reason);

                var gapResponse = new TeamRoleGapResponse(
                    roleTitle,
                    "AvailabilityGap",
                    reason,
                    primaryAllocation.ToDate.AddDays(1),
                    allocated.FullName,
                    allocated.EmployeeId,
                    primaryAllocation.FromDate,
                    primaryAllocation.ToDate,
                    primaryAllocation.ProjectName);

                return (gapResponse, alternative);
            }
        }

        if (isAvailabilityGap
            && !string.IsNullOrWhiteSpace(gap.AllocatedEmployeeName)
            && gap.AllocatedEmployeeId is null)
        {
            var byName = allocatedById.Values.FirstOrDefault(e =>
                e.FullName.Equals(gap.AllocatedEmployeeName, StringComparison.OrdinalIgnoreCase));

            if (byName is not null)
            {
                var primaryAllocation = byName.ActiveAllocations
                    .OrderByDescending(a => a.ToDate)
                    .FirstOrDefault();

                if (primaryAllocation is not null)
                {
                    var reason = $"{byName.FullName} has the required skills but is already allocated on " +
                                 $"{primaryAllocation.ProjectName} from {primaryAllocation.FromDate:yyyy-MM-dd} " +
                                 $"to {primaryAllocation.ToDate:yyyy-MM-dd}. Plan accordingly.";

                    var alternative = new TeamAllocatedAlternativeResponse(
                        roleTitle,
                        byName.EmployeeId,
                        byName.FullName,
                        primaryAllocation.ProjectName,
                        primaryAllocation.FromDate,
                        primaryAllocation.ToDate,
                        reason);

                    var gapResponse = new TeamRoleGapResponse(
                        roleTitle,
                        "AvailabilityGap",
                        reason,
                        primaryAllocation.ToDate.AddDays(1),
                        byName.FullName,
                        byName.EmployeeId,
                        primaryAllocation.FromDate,
                        primaryAllocation.ToDate,
                        primaryAllocation.ProjectName);

                    return (gapResponse, alternative);
                }
            }
        }

        return (new TeamRoleGapResponse(
            roleTitle,
            isAvailabilityGap ? "AvailabilityGap" : "SkillGap",
            gap.Reason,
            null,
            gap.AllocatedEmployeeName,
            gap.AllocatedEmployeeId), null);
    }

    private static AiTeamResult? ParseAiResponse(string raw)
    {
        try
        {
            var cleaned = ExtractJsonObject(CleanLlmText(raw));
            if (string.IsNullOrWhiteSpace(cleaned))
                return null;

            using var doc = JsonDocument.Parse(
                cleaned,
                new JsonDocumentOptions { AllowTrailingCommas = true });

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var root = doc.RootElement;
            var result = new AiTeamResult();

            if (TryGetProperty(root, "matches", out var matchesEl)
                && matchesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in matchesEl.EnumerateArray())
                {
                    var employeeId = ReadInt(item, "employeeId", "employee_id");
                    if (employeeId <= 0)
                        continue;

                    result.Matches.Add(new AiMatch
                    {
                        RoleTitle = ReadString(item, "roleTitle", "role_title") ?? string.Empty,
                        EmployeeId = employeeId,
                        Reason = ReadString(item, "reason") ?? string.Empty
                    });
                }
            }

            if (TryGetProperty(root, "gaps", out var gapsEl)
                && gapsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in gapsEl.EnumerateArray())
                {
                    result.Gaps.Add(new AiGap
                    {
                        RoleTitle = ReadString(item, "roleTitle", "role_title") ?? string.Empty,
                        GapType = NormalizeGapType(ReadString(item, "gapType", "gap_type")),
                        Reason = ReadString(item, "reason") ?? string.Empty,
                        AllocatedEmployeeId = ReadNullableInt(
                            item, "allocatedEmployeeId", "allocated_employee_id"),
                        AllocatedEmployeeName = ReadString(
                            item, "allocatedEmployeeName", "allocated_employee_name")
                    });
                }
            }

            return result.Matches.Count == 0 && result.Gaps.Count == 0 ? null : result;
        }
        catch
        {
            return null;
        }
    }

    private static string CleanLlmText(string raw) =>
        raw
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty)
            .Trim();

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
            return text.Trim();

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return text[start..(i + 1)];
            }
        }

        return text[start..].Trim();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
            return true;

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
                continue;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        return null;
    }

    private static int ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String
                && int.TryParse(value.GetString(), out number))
                return number;
        }

        return 0;
    }

    private static int? ReadNullableInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
                continue;

            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String
                && int.TryParse(value.GetString(), out number))
                return number;
        }

        return null;
    }

    private static string NormalizeGapType(string? gapType)
    {
        if (string.IsNullOrWhiteSpace(gapType))
            return "SkillGap";

        if (gapType.Contains("Availability", StringComparison.OrdinalIgnoreCase))
            return "AvailabilityGap";

        return "SkillGap";
    }

    private sealed class AiTeamResult
    {
        public List<AiMatch> Matches { get; set; } = [];
        public List<AiGap> Gaps { get; set; } = [];
    }

    private sealed class AiMatch
    {
        public string RoleTitle { get; set; } = string.Empty;
        public int EmployeeId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    private sealed class AiGap
    {
        public string RoleTitle { get; set; } = string.Empty;
        public string GapType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public int? AllocatedEmployeeId { get; set; }
        public string? AllocatedEmployeeName { get; set; }
    }
}
