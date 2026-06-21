using PRM.Application.Common;
using PRM.Application.DTOs.Employee;
using PRM.Application.DTOs.Manager;
using PRM.Application.DTOs.Project;
using PRM.Application.Interfaces.Service;
using PRM.Application.UseCases;
using PRM.Application.UseCases.Employees;
using PRM.Application.UseCases.Manager;
using PRM.Application.UseCases.Projects;
using PRM.Application.UseCases.Users;
using PRM.Domain.Entities;
using PRM.Domain.Enums;
using PRM.Tests.Helpers;

namespace PRM.Tests.Additional;

public class OrganizationValuesTests
{
    [Fact]
    public void ValidateDepartment_AcceptsKnownValue()
    {
        OrganizationValues.ValidateDepartment("Development").Should().BeNull();
    }

    [Fact]
    public void ValidateDesignation_RejectsUnknownValue()
    {
        OrganizationValues.ValidateDesignation("Director").Should().NotBeNull();
    }
}

public class UpdateProjectUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UpdatesProject_WhenValid()
    {
        var uow = new InMemoryUnitOfWork();
        var manager = TestDataBuilder.CreateUser(1, UserRole.Manager);
        uow.Users.Seed(manager);
        uow.Employees.Seed(manager.Employee!);
        uow.Projects.Seed(TestDataBuilder.CreateProject(10, 1));

        var useCase = new UpdateProjectUseCase(uow);
        var result = await useCase.ExecuteAsync(10, new UpdateProjectRequest(
            "Updated Name", "Desc",
            TestDataBuilder.Past(7), TestDataBuilder.Future(60),
            "Active", 1));

        result.IsSuccess.Should().BeTrue();
        uow.Projects.Items[0].Name.Should().Be("Updated Name");
    }
}

public class UserStatusUseCaseTests
{
    [Fact]
    public async Task DeactivateUser_Succeeds_ForActiveUser()
    {
        var uow = new InMemoryUnitOfWork();
        var user = TestDataBuilder.CreateUser(1, UserRole.Admin);
        uow.Users.Seed(user);

        var useCase = new DeactivateUserUseCase(uow);
        var result = await useCase.ExecuteAsync(user.Username);

        result.IsSuccess.Should().BeTrue();
        uow.Users.Items[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ReactivateUser_Succeeds_ForInactiveUser()
    {
        var uow = new InMemoryUnitOfWork();
        var user = TestDataBuilder.CreateUser(1, UserRole.Admin, isActive: false);
        uow.Users.Seed(user);

        var useCase = new ReactivateUserUseCase(uow);
        var result = await useCase.ExecuteAsync(user.Username);

        result.IsSuccess.Should().BeTrue();
        uow.Users.Items[0].IsActive.Should().BeTrue();
    }
}

public class AssignManagerUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_AssignsManager_WhenValid()
    {
        var uow = new InMemoryUnitOfWork();
        var manager = TestDataBuilder.CreateUser(1, UserRole.Manager);
        var employee = TestDataBuilder.CreateUser(2, UserRole.Employee, managerId: 99);
        uow.Users.Seed(manager, employee);
        uow.Employees.Seed(manager.Employee!, employee.Employee!);

        var useCase = new AssignManagerUseCase(uow);
        var result = await useCase.ExecuteAsync(2, 1);

        result.IsSuccess.Should().BeTrue();
        uow.Employees.Items.Single(e => e.Id == 2).ManagerId.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenSelfAssignment()
    {
        var uow = new InMemoryUnitOfWork();
        var employee = TestDataBuilder.CreateUser(2, UserRole.Employee);
        uow.Users.Seed(employee);
        uow.Employees.Seed(employee.Employee!);

        var useCase = new AssignManagerUseCase(uow);
        var result = await useCase.ExecuteAsync(2, 2);

        result.IsSuccess.Should().BeFalse();
    }
}

public class GetResourceDashboardUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_SplitsBenchAndActiveEmployees()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetTeamEmployeesForDashboardAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TeamEmployeeDashboardRow(2, "Bench User", "Development", "SE", "Bench", 0),
                new TeamEmployeeDashboardRow(3, "Busy User", "Development", "SE", "Allocated", 80)
            ]);
        query.Setup(q => q.GetEmployeeWithSkillsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeDetailResponse(
                2, "Bench User", "dev@intimetec.com", "Development", "SE",
                "Employee", "Bench", true, []));

        var useCase = new GetResourceDashboardUseCase(query.Object);
        var result = await useCase.ExecuteAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Data!.BenchCount.Should().Be(1);
        result.Data.ActiveEmployees.Should().HaveCount(1);
    }
}

public class GetActiveAllocationsForWeekUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsAllocations_WhenWeekValid()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Users.Seed(TestDataBuilder.CreateUser(2, UserRole.Employee));
        uow.SystemConfigurations.Seed(TestDataBuilder.DefaultConfig());
        uow.Allocations.Seed(TestDataBuilder.CreateAllocation(
            1, 2, 10, 50, TestDataBuilder.Past(7), TestDataBuilder.Future(7)));

        var monday = ValidationRules.ToMonday(TestDataBuilder.Today.AddDays(-7));

        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetActiveAllocationsForWeekAsync(2, monday, 40, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActiveAllocationForSubmitResponse(10, "Project 10", 50, 20)]);

        var useCase = new GetActiveAllocationsForWeekUseCase(uow, query.Object);
        var result = await useCase.ExecuteAsync(2, monday);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Allocations.Should().HaveCount(1);
    }
}

public class GetMyProjectsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsManagerProjects()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetManagerProjectsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ManagerProjectSummaryResponse(10, "Alpha", TestDataBuilder.Future(30), "OnTrack")]);

        var useCase = new GetMyProjectsUseCase(query.Object);
        var result = await useCase.ExecuteAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().HaveCount(1);
    }
}

public class GetTeamTimesheetsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsTeamTimesheets()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetTeamTimesheetsAsync(1, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamTimesheetResponse(TestDataBuilder.Today, []));

        var useCase = new GetTeamTimesheetsUseCase(query.Object);
        var result = await useCase.ExecuteAsync(1, TestDataBuilder.Today);

        result.IsSuccess.Should().BeTrue();
    }
}

public class UpdateSkillProficiencyUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UpdatesProficiency()
    {
        var uow = new InMemoryUnitOfWork();
        uow.EmployeeSkills.Seed(new EmployeeSkill
        {
            Id = 1,
            EmployeeId = 2,
            SkillName = "Java",
            Category = SkillCategory.Backend,
            Proficiency = ProficiencyLevel.Beginner
        });

        var useCase = new UpdateSkillProficiencyUseCase(uow);
        var result = await useCase.ExecuteAsync(1, new UpdateSkillProficiencyRequest("Advanced"));

        result.IsSuccess.Should().BeTrue();
        uow.EmployeeSkills.Items[0].Proficiency.Should().Be(ProficiencyLevel.Advanced);
    }
}

public class GetAllUsersUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsAllUsersWithCounts()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Users.Seed(
            TestDataBuilder.CreateUser(1, UserRole.Admin),
            TestDataBuilder.CreateUser(2, UserRole.Employee, isActive: false));

        var useCase = new GetAllUsersUseCase(uow);
        var result = await useCase.ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(2);
        result.Data.ActiveCount.Should().Be(1);
        result.Data.InactiveCount.Should().Be(1);
    }
}
