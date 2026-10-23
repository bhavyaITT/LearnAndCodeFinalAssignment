using PRM.Application.Common;
using PRM.Application.DTOs.Employee;
using PRM.Application.Interfaces.Service;
using PRM.Application.UseCases.Employees;
using PRM.Domain.Entities;
using PRM.Domain.Enums;
using PRM.Tests.Helpers;

namespace PRM.Tests.Phase4;

public class SubmitTimesheetUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_SubmitsTimesheet_WhenRulesPass()
    {
        var uow = new InMemoryUnitOfWork();
        var employee = TestDataBuilder.CreateUser(2, UserRole.Employee);
        uow.Users.Seed(employee);
        uow.SystemConfigurations.Seed(TestDataBuilder.DefaultConfig());
        uow.Projects.Seed(TestDataBuilder.CreateProject(10, 1));
        uow.Allocations.Seed(TestDataBuilder.CreateAllocation(
            1, 2, 10, 50, TestDataBuilder.Past(7), TestDataBuilder.Future(7)));

        var monday = ValidationRules.ToMonday(TestDataBuilder.Today.AddDays(-7));

        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetActiveAllocationsForWeekAsync(2, monday, 40, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ActiveAllocationForSubmitResponse(10, "Project 10", 50, 20)
            ]);

        var useCase = new SubmitTimesheetUseCase(uow, query.Object);
        var result = await useCase.ExecuteAsync(2, new SubmitTimesheetRequest(
            monday,
            [new TimesheetEntryRequest(10, 8, "Java, Microservices")]));

        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalHours.Should().Be(8);
        uow.Timesheets.Items.Should().HaveCount(1);
        uow.TimesheetEntries.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenSubmissionFrozen()
    {
        var uow = new InMemoryUnitOfWork();
        var employee = TestDataBuilder.CreateUser(2, UserRole.Employee);
        employee.TimesheetSubmissionFrozen = true;
        uow.Users.Seed(employee);

        var useCase = new SubmitTimesheetUseCase(uow, Mock.Of<IQueryService>());
        var result = await useCase.ExecuteAsync(2, new SubmitTimesheetRequest(
            TestDataBuilder.Today, [new TimesheetEntryRequest(10, 8, "Java")]));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("frozen");
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenMissedRecordExists()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Users.Seed(TestDataBuilder.CreateUser(2, UserRole.Employee));
        var monday = ValidationRules.ToMonday(TestDataBuilder.Today.AddDays(-7));
        uow.Timesheets.Seed(new Timesheet
        {
            Id = 1,
            EmployeeId = 2,
            WeekStartDate = monday,
            TotalHours = 0,
            Status = TimesheetStatus.Missed
        });

        var useCase = new SubmitTimesheetUseCase(uow, Mock.Of<IQueryService>());
        var result = await useCase.ExecuteAsync(2, new SubmitTimesheetRequest(
            monday, [new TimesheetEntryRequest(10, 8, "Java")]));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("missed");
    }
}

public class CheckMissedTimesheetUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsFrozenState_WhenSubmissionFrozen()
    {
        var uow = new InMemoryUnitOfWork();
        var employee = TestDataBuilder.CreateUser(2, UserRole.Employee);
        employee.TimesheetSubmissionFrozen = true;
        employee.PendingMissedWeekStart = TestDataBuilder.Past(14);
        uow.Users.Seed(employee);

        var useCase = new CheckMissedTimesheetUseCase(uow);
        var result = await useCase.ExecuteAsync(2);

        result.IsSuccess.Should().BeTrue();
        result.Data!.IsMissing.Should().BeTrue();
        result.Data.IsSubmissionFrozen.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotMissing_WhenNoAllocationLastWeek()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Users.Seed(TestDataBuilder.CreateUser(2, UserRole.Employee));

        var useCase = new CheckMissedTimesheetUseCase(uow);
        var result = await useCase.ExecuteAsync(2);

        result.IsSuccess.Should().BeTrue();
        result.Data!.IsMissing.Should().BeFalse();
    }
}

public class GetMyTimesheetsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsTimesheetsFromQueryService()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetMyTimesheetsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TimesheetSummaryResponse>());

        var useCase = new GetMyTimesheetsUseCase(query.Object);
        var result = await useCase.ExecuteAsync(2);

        result.IsSuccess.Should().BeTrue();
    }
}

public class GetMyAllocationsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsAllocationsFromQueryService()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetMyAllocationsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MyAllocationsResponse([], 0));

        var useCase = new GetMyAllocationsUseCase(query.Object);
        var result = await useCase.ExecuteAsync(2);

        result.IsSuccess.Should().BeTrue();
    }
}
