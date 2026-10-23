using PRM.Application.Interfaces.Service;

namespace PRM.Application.UseCases.AI;

public static class TeamSkillMatchPromptBuilder
{
    public static string BuildSystemPrompt() =>
        """
        You are a resource staffing assistant for a software delivery company.
        Your job is to read a manager's plain-English team requirement, infer the roles
        needed, and staff the team in ONE pass.

        You will receive:
        1. The manager's requirement (skills, team size, roles, duration, etc.)
        2. BENCH employees — the ONLY people you may assign as matches
        3. ALLOCATED employees — skilled people already on projects (for availability gaps only)

        Steps:
        1. Infer the distinct roles the project needs (e.g. Backend Developer, QA Engineer, Scrum Master).
        2. For each role, pick the best-matching BENCH employee as a match, OR report a gap.

        Return ONLY valid JSON with this exact shape (no markdown, no extra text):
        {
          "matches": [
            {
              "roleTitle": "QA Engineer",
              "employeeId": 6,
              "reason": "Yugansh has Testing skills and is on bench."
            }
          ],
          "gaps": [
            {
              "roleTitle": "Java Developer",
              "gapType": "AvailabilityGap",
              "reason": "Hari has Java skills but is already allocated on another project.",
              "allocatedEmployeeId": 4,
              "allocatedEmployeeName": "Hari"
            },
            {
              "roleTitle": "Scrum Master",
              "gapType": "SkillGap",
              "reason": "No bench or allocated employee has scrum master skills.",
              "allocatedEmployeeId": null,
              "allocatedEmployeeName": null
            }
          ]
        }

        Rules:
        - gapType must be exactly "SkillGap" or "AvailabilityGap" (one value only, never both)
        - employeeId and allocatedEmployeeId must be JSON numbers, not strings
        - Assign at most ONE bench employee per role in matches
        - NEVER assign the same employeeId to more than one role
        - Only use employeeId values from the BENCH list in matches
        - If a suitable person exists on BENCH, add a match (not a gap)
        - If no bench employee fits but an ALLOCATED employee has the required skills, add a gap with:
          gapType "AvailabilityGap", allocatedEmployeeId, allocatedEmployeeName, and reason explaining
          they are already allocated — manager should plan around their allocation dates shown in input
        - If nobody in the organisation fits the role, add gapType "SkillGap"
        - Every inferred role must appear exactly once in matches OR gaps
        - Do not invent employees, skills, or dates not present in the input
        """;

    public static string BuildUserPrompt(
        string requirementText,
        string? projectContext,
        IEnumerable<BenchEmployeeAiContext> benchEmployees,
        IEnumerable<AllocatedEmployeeAiContext> allocatedEmployees)
    {
        var benchLines = benchEmployees.Select((e, i) =>
        {
            var skillText = e.Skills.Any()
                ? string.Join("; ", e.Skills.Select(s => $"{s.SkillName} ({s.Proficiency})"))
                : "No skills on profile";
            return $"""
            Bench {i + 1}:
              ID: {e.EmployeeId}
              Name: {e.FullName}
              Department: {e.Department} | Designation: {e.Designation}
              Skills: {skillText}
            """;
        });

        var allocatedLines = allocatedEmployees.Select((e, i) =>
        {
            var skillText = e.Skills.Any()
                ? string.Join("; ", e.Skills.Select(s => $"{s.SkillName} ({s.Proficiency})"))
                : "No skills on profile";
            var allocationText = e.ActiveAllocations.Any()
                ? string.Join("; ", e.ActiveAllocations.Select(a =>
                    $"{a.ProjectName} ({a.FromDate:yyyy-MM-dd} to {a.ToDate:yyyy-MM-dd}, {a.UtilisationPercentage}%)"))
                : "No active allocation dates on record";
            return $"""
            Allocated {i + 1}:
              ID: {e.EmployeeId}
              Name: {e.FullName}
              Department: {e.Department} | Designation: {e.Designation}
              Skills: {skillText}
              Current allocations: {allocationText}
            """;
        });

        var projectLine = string.IsNullOrWhiteSpace(projectContext)
            ? string.Empty
            : $"Project context: {projectContext}\n\n";

        var allocatedSection = allocatedEmployees.Any()
            ? $"""
              ALLOCATED employees (do NOT assign — use only for AvailabilityGap when skills match):
              {string.Join("\n\n", allocatedLines)}
              """
            : """
              ALLOCATED employees: (none currently allocated organisation-wide)
              """;

        return $"""
        {projectLine}Team requirement:
        {requirementText.Trim()}

        BENCH employees (assignable candidates):
        {string.Join("\n\n", benchLines.Any() ? benchLines : ["  (none on bench)"])}

        {allocatedSection}

        Infer the roles needed, then return the JSON object now.
        """;
    }
}
