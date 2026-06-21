using PRM.Application.DTOs;
using PRM.Application.DTOs.Manager;
using PRM.Application.Interfaces.Service;
using PRM.Application.UseCases.Manager;
using PRM.Application.UseCases.SystemConfig;
using PRM.Domain.Entities;
using PRM.Domain.Enums;
using PRM.Tests.Helpers;

namespace PRM.Tests.Phase3;

public class ResolveEmployeeIdUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsEmployeeId_WhenProfileExists()
    {
        var uow = new InMemoryUnitOfWork();
        var manager = TestDataBuilder.CreateUser(1, UserRole.Manager);
        uow.Employees.Seed(manager.Employee!);

        var useCase = new ResolveEmployeeIdUseCase(uow);
        var result = await useCase.ExecuteAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenProfileMissing()
    {
        var useCase = new ResolveEmployeeIdUseCase(new InMemoryUnitOfWork());
        var result = await useCase.ExecuteAsync(99);

        result.IsSuccess.Should().BeFalse();
    }
}

public class ValidateAllocationUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsValid_WhenUtilisationWithinLimit()
    {
        var uow = new InMemoryUnitOfWork();
        SeedAllocationScenario(uow, out var managerId, out var employeeId, out var projectId);

        var query = new Mock<IQueryService>();
        query.Setup(q => q.HasOverlappingAllocationOnProjectAsync(
                employeeId, projectId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        query.Setup(q => q.GetEmployeeUtilisationInPeriodAsync(
                employeeId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(20);

        var request = new CreateAllocationRequest(
            employeeId, projectId, 50, TestDataBuilder.Today, TestDataBuilder.Future(30));

        var useCase = new ValidateAllocationUseCase(uow, query.Object);
        var result = await useCase.ExecuteAsync(request, managerId);

        result.IsSuccess.Should().BeTrue();
        result.Data!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsInvalid_WhenOverAllocated()
    {
        var uow = new InMemoryUnitOfWork();
        SeedAllocationScenario(uow, out var managerId, out var employeeId, out var projectId);

        var query = new Mock<IQueryService>();
        query.Setup(q => q.HasOverlappingAllocationOnProjectAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        query.Setup(q => q.GetEmployeeUtilisationInPeriodAsync(
                employeeId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(80);

        var request = new CreateAllocationRequest(
            employeeId, projectId, 30, TestDataBuilder.Today, TestDataBuilder.Future(30));

        var useCase = new ValidateAllocationUseCase(uow, query.Object);
        var result = await useCase.ExecuteAsync(request, managerId);

        result.IsSuccess.Should().BeTrue();
        result.Data!.IsValid.Should().BeFalse();
    }

    private static void SeedAllocationScenario(
        InMemoryUnitOfWork uow, out int managerId, out int employeeId, out int projectId)
    {
        managerId = 1;
        employeeId = 2;
        projectId = 10;

        var manager = TestDataBuilder.CreateUser(managerId, UserRole.Manager);
        var employee = TestDataBuilder.CreateUser(employeeId, UserRole.Employee, managerId: managerId);
        uow.Users.Seed(manager, employee);
        uow.Employees.Seed(manager.Employee!, employee.Employee!);
        uow.Projects.Seed(TestDataBuilder.CreateProject(projectId, managerId));
    }
}

public class CreateAllocationUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesAllocation_AndUpdatesBenchEmployee()
    {
        var uow = new InMemoryUnitOfWork();
        var manager = TestDataBuilder.CreateUser(1, UserRole.Manager);
        var employee = TestDataBuilder.CreateUser(2, UserRole.Employee, managerId: 1);
        uow.Users.Seed(manager, employee);
        uow.Employees.Seed(manager.Employee!, employee.Employee!);
        uow.Projects.Seed(TestDataBuilder.CreateProject(10, 1));

        var query = new Mock<IQueryService>();
        query.Setup(q => q.HasOverlappingAllocationOnProjectAsync(
                2, 10, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        query.Setup(q => q.GetEmployeeUtilisationInPeriodAsync(
                2, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var health = new Mock<IProjectHealthService>();
        health.Setup(h => h.RecalculateHealthAsync(10, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CreateAllocationRequest(
            2, 10, 50, TestDataBuilder.Today, TestDataBuilder.Future(30));

        var useCase = new CreateAllocationUseCase(uow, query.Object, health.Object);
        var result = await useCase.ExecuteAsync(request, 1);

        result.IsSuccess.Should().BeTrue();
        uow.Allocations.Items.Should().HaveCount(1);
        uow.Employees.Items.Single(e => e.Id == 2).Status.Should().Be(EmployeeStatus.Allocated);
    }
}

public class EndAllocationUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShortensAllocation_AndSetsEmployeeToBench()
    {
        var uow = new InMemoryUnitOfWork();
        var manager = TestDataBuilder.CreateUser(1, UserRole.Manager);
        var employee = TestDataBuilder.CreateUser(2, UserRole.Employee, managerId: 1);
        employee.Employee!.Status = EmployeeStatus.Allocated;
        uow.Users.Seed(manager, employee);
        uow.Employees.Seed(manager.Employee!, employee.Employee!);
        uow.Projects.Seed(TestDataBuilder.CreateProject(10, 1));
        uow.Allocations.Seed(TestDataBuilder.CreateAllocation(
            5, 2, 10, 50, TestDataBuilder.Past(7), TestDataBuilder.Future(30)));

        var useCase = new EndAllocationUseCase(uow);
        var result = await useCase.ExecuteAsync(5, 1);

        result.IsSuccess.Should().BeTrue();
        result.Data!.EmployeeNewStatus.Should().Be(nameof(EmployeeStatus.Bench));
        uow.Allocations.Items[0].ToDate.Should().Be(TestDataBuilder.Today.AddDays(-1));
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenNotManagersProject()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Projects.Seed(TestDataBuilder.CreateProject(10, 1));
        uow.Allocations.Seed(TestDataBuilder.CreateAllocation(5, 2, 10, 50));

        var useCase = new EndAllocationUseCase(uow);
        var result = await useCase.ExecuteAsync(5, 99);

        result.IsSuccess.Should().BeFalse();
    }
}

public class RestoreTimesheetSubmissionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UnfreezesUser_AndRemovesMissedTimesheet()
    {
        var uow = new InMemoryUnitOfWork();
        var employee = TestDataBuilder.CreateUser(2, UserRole.Employee);
        employee.TimesheetSubmissionFrozen = true;
        employee.PendingMissedWeekStart = TestDataBuilder.Past(14);
        uow.Users.Seed(employee);
        uow.Employees.Seed(employee.Employee!);
        uow.Timesheets.Seed(new Timesheet
        {
            Id = 1,
            EmployeeId = 2,
            WeekStartDate = employee.PendingMissedWeekStart.Value,
            TotalHours = 0,
            Status = TimesheetStatus.Missed
        });

        var query = new Mock<IQueryService>();
        query.Setup(q => q.IsEmployeeUnderManagerAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new RestoreTimesheetSubmissionUseCase(uow, query.Object);
        var result = await useCase.ExecuteAsync(2, 1);

        result.IsSuccess.Should().BeTrue();
        employee.TimesheetSubmissionFrozen.Should().BeFalse();
        uow.Timesheets.Items.Should().BeEmpty();
    }
}

public class SystemConfigUseCaseTests
{
    [Fact]
    public async Task GetSystemConfig_ReturnsConfig_WhenPresent()
    {
        var uow = new InMemoryUnitOfWork();
        uow.SystemConfigurations.Seed(TestDataBuilder.DefaultConfig());

        var useCase = new GetSystemConfigUseCase(uow);
        var result = await useCase.ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Data!.MaxWeeklyHours.Should().Be(40);
    }

    [Fact]
    public async Task UpdateSystemConfig_RejectsInvalidProvider()
    {
        var uow = new InMemoryUnitOfWork();
        uow.SystemConfigurations.Seed(TestDataBuilder.DefaultConfig());

        var scheduler = new Mock<ISchedulerService>();
        var useCase = new UpdateSystemConfigUseCase(uow, scheduler.Object);
        var result = await useCase.ExecuteAsync(
            new UpdateSystemConfigRequest("InvalidProvider", null, null, null));

        result.IsSuccess.Should().BeFalse();
    }
}
