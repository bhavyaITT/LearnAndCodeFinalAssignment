namespace PRM.Application.Common;

public static class OrganizationValues
{
    public static readonly string[] Departments =
        ["Development", "HR", "Management", "Administration", "Testing", "SDET"];

    public static readonly string[] Designations =
        ["SE", "Manager", "Admin"];

    public static string? ValidateDepartment(string? department)
    {
        if (string.IsNullOrWhiteSpace(department))
            return "Department is required.";

        if (!Departments.Contains(department, StringComparer.OrdinalIgnoreCase))
            return $"Invalid department. Allowed: {string.Join(", ", Departments)}.";

        return null;
    }

    public static string? ValidateDesignation(string? designation)
    {
        if (string.IsNullOrWhiteSpace(designation))
            return "Designation is required.";

        if (!Designations.Contains(designation, StringComparer.OrdinalIgnoreCase))
            return $"Invalid designation. Allowed: {string.Join(", ", Designations)}.";

        return null;
    }

    public static string NormalizeDepartment(string department) =>
        Departments.First(d => d.Equals(department, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeDesignation(string designation) =>
        Designations.First(d => d.Equals(designation, StringComparison.OrdinalIgnoreCase));
}
