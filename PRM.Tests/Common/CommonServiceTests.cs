using PRM.Application.Common;
using PRM.Domain.Entities;
using PRM.Domain.Enums;
using PRM.Tests.Helpers;

namespace PRM.Tests.Common;

public class ValidationRulesTests
{
    [Fact]
    public void IsIntimetecEmail_AcceptsValidEmail()
    {
        ValidationRules.IsIntimetecEmail("john.doe@intimetec.com").Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@intimetec.com")]
    [InlineData("user@gmail.com")]
    public void IsIntimetecEmail_RejectsInvalidEmail(string email)
    {
        ValidationRules.IsIntimetecEmail(email).Should().BeFalse();
    }

    [Fact]
    public void ValidateUtilisation_AcceptsValidRange()
    {
        ValidationRules.ValidateUtilisation(50).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ValidateUtilisation_RejectsOutOfRange(int value)
    {
        ValidationRules.ValidateUtilisation(value).Should().NotBeNull();
    }

    [Fact]
    public void ValidateAllocationDateRange_RejectsPastStart()
    {
        var from = TestDataBuilder.Past(1);
        var to = TestDataBuilder.Future(30);

        ValidationRules.ValidateAllocationDateRange(from, to).Should().Contain("past");
    }

    [Fact]
    public void ValidateTimesheetWeek_RejectsFutureWeek()
    {
        var futureMonday = TestDataBuilder.Future(14);
        ValidationRules.ValidateTimesheetWeek(futureMonday).Should().Contain("future");
    }

    [Fact]
    public void ToMonday_NormalizesWednesdayToMonday()
    {
        var wednesday = new DateOnly(2026, 6, 17);
        ValidationRules.ToMonday(wednesday).Should().Be(new DateOnly(2026, 6, 15));
    }
}

public class WorkingDayHelperTests
{
    [Fact]
    public void IsWorkingDay_ReturnsFalseForSaturday()
    {
        var saturday = new DateOnly(2026, 6, 20);
        WorkingDayHelper.IsWorkingDay(saturday).Should().BeFalse();
    }

    [Fact]
    public void CountWorkingDays_CountsMonToFri()
    {
        var monday = new DateOnly(2026, 6, 15);
        var friday = new DateOnly(2026, 6, 19);

        WorkingDayHelper.CountWorkingDays(monday, friday).Should().Be(5);
    }

    [Fact]
    public void GetLastCompletedWeekMonday_ReturnsPreviousWeekMonday()
    {
        var wednesday = new DateOnly(2026, 6, 17);
        WorkingDayHelper.GetLastCompletedWeekMonday(wednesday)
            .Should().Be(new DateOnly(2026, 6, 8));
    }
}

public class AllocationDateHelperTests
{
    [Fact]
    public void IsActiveOnDate_ReturnsTrueWhenDateWithinRange()
    {
        var allocation = TestDataBuilder.CreateAllocation(1, 2, 3, 50);
        AllocationDateHelper.IsActiveOnDate(allocation, TestDataBuilder.Today).Should().BeTrue();
    }

    [Fact]
    public void IsActiveOnDate_ReturnsFalseWhenRangeInvalid()
    {
        var allocation = TestDataBuilder.CreateAllocation(1, 2, 3, 50);
        allocation.ToDate = allocation.FromDate.AddDays(-1);

        AllocationDateHelper.IsActiveOnDate(allocation, TestDataBuilder.Today).Should().BeFalse();
    }

    [Fact]
    public void IsEndable_ReturnsFalseForPastAllocation()
    {
        var allocation = TestDataBuilder.CreateAllocation(
            1, 2, 3, 50, TestDataBuilder.Past(30), TestDataBuilder.Past(1));

        AllocationDateHelper.IsEndable(allocation, TestDataBuilder.Today).Should().BeFalse();
    }
}

public class ProjectHealthCalculatorTests
{
    [Fact]
    public void ComputeHealth_ReturnsAtRisk_WhenMilestoneOverdue()
    {
        var project = TestDataBuilder.CreateProject(1, 1);
        project.Milestones.Add(new Milestone
        {
            Id = 1,
            ProjectId = 1,
            Title = "Release",
            DueDate = TestDataBuilder.Past(1),
            Status = MilestoneStatus.NotStarted
        });

        ProjectHealthCalculator.ComputeHealth(project, TestDataBuilder.Today, 40)
            .Should().Be(ProjectHealth.AtRisk);
    }

    [Fact]
    public void ComputeHealth_ReturnsOnTrack_WhenNoIssues()
    {
        var project = TestDataBuilder.CreateProject(1, 1);
        project.Milestones.Add(new Milestone
        {
            Id = 1,
            ProjectId = 1,
            Title = "Release",
            DueDate = TestDataBuilder.Future(30),
            Status = MilestoneStatus.InProgress
        });

        ProjectHealthCalculator.ComputeHealth(project, TestDataBuilder.Today, 40)
            .Should().Be(ProjectHealth.OnTrack);
    }
}
