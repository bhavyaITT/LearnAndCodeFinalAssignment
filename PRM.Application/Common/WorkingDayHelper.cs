namespace PRM.Application.Common;

public static class WorkingDayHelper
{
    public static bool IsWorkingDay(DateOnly date) =>
        date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;

    /// <summary>Counts Mon–Fri days from the day after start through end inclusive.</summary>
    public static int CountWorkingDays(DateOnly startInclusive, DateOnly endInclusive)
    {
        if (endInclusive < startInclusive)
            return 0;

        var count = 0;
        for (var date = startInclusive; date <= endInclusive; date = date.AddDays(1))
        {
            if (IsWorkingDay(date))
                count++;
        }

        return count;
    }

    public static DateOnly GetLastCompletedWeekMonday(DateOnly today)
    {
        var dayOfWeek = (int)today.DayOfWeek;
        var daysToThisMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return today.AddDays(-daysToThisMonday - 7);
    }
}
