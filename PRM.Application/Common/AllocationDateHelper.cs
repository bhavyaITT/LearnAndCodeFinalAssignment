using PRM.Domain.Entities;

namespace PRM.Application.Common;

public static class AllocationDateHelper
{
    /// <summary>An allocation is active on a given day when it overlaps that date.</summary>
    public static bool IsActiveOnDate(Allocation allocation, DateOnly date) =>
        IsValidRange(allocation)
        && allocation.FromDate <= date
        && allocation.ToDate >= date;

    public static bool IsValidRange(Allocation allocation) =>
        allocation.ToDate >= allocation.FromDate;

    /// <summary>Allocation has not fully ended yet and has a valid date range.</summary>
    public static bool IsEndable(Allocation allocation, DateOnly today) =>
        IsValidRange(allocation) && allocation.ToDate >= today;
}
