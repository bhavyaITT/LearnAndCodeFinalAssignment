namespace PRM.Application.Common;

public static class ValidationRules
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static bool IsIntimetecEmail(string email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.EndsWith("@intimetec.com", StringComparison.OrdinalIgnoreCase)
        && email.Length > "@intimetec.com".Length
        && !email.Contains(' ');

    public static string? ValidateNonEmptyName(string? name, string fieldName = "Name")
    {
        if (string.IsNullOrWhiteSpace(name))
            return $"{fieldName} is required.";
        return null;
    }

    public static string? ValidateProjectDatesForCreate(DateOnly startDate, DateOnly endDate)
    {
        if (startDate < Today)
            return "Start date cannot be in the past.";

        if (endDate < Today)
            return "End date must be in the future.";

        if (endDate <= startDate)
            return "End date must be after start date.";

        return null;
    }

    public static string? ValidateProjectDatesForUpdate(
        DateOnly startDate, DateOnly endDate, DateOnly existingStartDate)
    {
        if (endDate <= startDate)
            return "End date must be after start date.";

        if (startDate < Today && startDate != existingStartDate)
            return "Start date cannot be moved to a past date.";

        return null;
    }

    public static string? ValidateMilestoneDueDate(DateOnly dueDate, DateOnly projectEnd)
    {
        if (dueDate > projectEnd)
            return $"Milestone due date cannot be after the project end date ({projectEnd:dd-MM-yyyy}).";

        return null;
    }

    public static string? ValidateUtilisation(int utilisation) =>
        utilisation is >= 1 and <= 100
            ? null
            : "Utilisation must be between 1 and 100.";

    public static string? ValidateAllocationDateRange(DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate >= toDate)
            return "From date must be before To date.";

        if (fromDate < Today)
            return "Allocation cannot start in the past.";

        return null;
    }

    public static string? ValidateAllocationWithinProject(
        DateOnly fromDate, DateOnly toDate, DateOnly projectStart, DateOnly projectEnd) =>
        fromDate < projectStart || toDate > projectEnd
            ? $"Allocation dates must fall within the project period ({projectStart:dd-MM-yyyy} to {projectEnd:dd-MM-yyyy})."
            : null;

    public static string? ValidateTimesheetWeek(DateOnly weekStartDate)
    {
        var monday = ToMonday(weekStartDate);

        if (monday > Today)
            return "You cannot submit a timesheet for a future week.";

        return null;
    }

    public static DateOnly WeekEnd(DateOnly weekMonday) => weekMonday.AddDays(6);

    public static DateOnly ToMonday(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-daysFromMonday);
    }
}
