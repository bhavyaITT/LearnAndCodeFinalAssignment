using PRM.Application.DTOs.Employee;
using PRM.Application.DTOs.Project;
using PRM.Application.DTOs.Users;
using PRM.Application.Interfaces.Service;
using PRM.Application.UseCases;
using PRM.Application.UseCases.Projects;
using PRM.Application.UseCases.Users;
using PRM.Domain.Entities;
using PRM.Domain.Enums;
using PRM.Tests.Helpers;

namespace PRM.Tests.Phase2;

public class CreateUserUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesUser_WhenRequestValid()
    {
        var uow = new InMemoryUnitOfWork();
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        var useCase = new CreateUserUseCase(uow, hasher.Object);
        var result = await useCase.ExecuteAsync(1, new CreateUserRequest(
            "Jane Doe", "jane.doe@intimetec.com", "jane.doe",
            "TempPass1", "Employee", "Development", "SE"));

        result.IsSuccess.Should().BeTrue();
        result.Data!.Username.Should().Be("jane.doe");
        uow.Users.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenEmailInvalid()
    {
        var useCase = new CreateUserUseCase(new InMemoryUnitOfWork(), Mock.Of<IPasswordHasher>());
        var result = await useCase.ExecuteAsync(1, new CreateUserRequest(
            "Jane Doe", "bad-email", "jane", "TempPass1", "Employee", "Development", "SE"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("@intimetec.com");
    }
}

public class ProjectUseCaseTests
{
    [Fact]
    public async Task CreateProject_Succeeds_WithValidManager()
    {
        var uow = new InMemoryUnitOfWork();
        var manager = TestDataBuilder.CreateUser(1, UserRole.Manager);
        uow.Users.Seed(manager);
        uow.Employees.Seed(manager.Employee!);
        await uow.SaveChangesAsync();

        var useCase = new CreateProjectUseCase(uow);
        var result = await useCase.ExecuteAsync(new CreateProjectRequest(
            "Alpha Portal", "Backend work",
            TestDataBuilder.Today, TestDataBuilder.Future(60),
            "Active", 1));

        result.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("Alpha Portal");
        uow.Projects.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateProject_Fails_WhenEndDateBeforeStartDate()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Users.Seed(TestDataBuilder.CreateUser(1, UserRole.Manager));
        uow.Employees.Seed(TestDataBuilder.CreateUser(1, UserRole.Manager).Employee!);

        var useCase = new CreateProjectUseCase(uow);
        var result = await useCase.ExecuteAsync(new CreateProjectRequest(
            "Bad Project", "Desc",
            TestDataBuilder.Future(30), TestDataBuilder.Today,
            "Active", 1));

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetProjectDetail_Fails_WhenProjectMissing()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetProjectWithMilestonesAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectDetailResponse?)null);

        var useCase = new GetProjectDetailUseCase(query.Object);
        var result = await useCase.ExecuteAsync(99);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }
}

public class MilestoneUseCaseTests
{
    [Fact]
    public async Task AddMilestone_Succeeds_WhenDueDateWithinProject()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Projects.Seed(TestDataBuilder.CreateProject(1, 1));

        var useCase = new AddMilestoneUseCase(uow);
        var result = await useCase.ExecuteAsync(1, new AddMilestoneRequest("Sprint 1", TestDataBuilder.Future(30)));

        result.IsSuccess.Should().BeTrue();
        uow.Milestones.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateMilestoneStatus_Succeeds_WithValidStatus()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Milestones.Seed(new Milestone
        {
            Id = 1,
            ProjectId = 1,
            Title = "Sprint 1",
            DueDate = TestDataBuilder.Future(10),
            Status = MilestoneStatus.NotStarted
        });

        var useCase = new UpdateMilestoneStatusUseCase(uow);
        var result = await useCase.ExecuteAsync(1, new UpdateMilestoneStatusRequest("InProgress"));

        result.IsSuccess.Should().BeTrue();
        uow.Milestones.Items[0].Status.Should().Be(MilestoneStatus.InProgress);
    }

    [Fact]
    public async Task UpdateMilestoneDueDate_Fails_WhenAfterProjectEnd()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Projects.Seed(TestDataBuilder.CreateProject(1, 1, end: TestDataBuilder.Future(10)));
        uow.Milestones.Seed(new Milestone
        {
            Id = 1,
            ProjectId = 1,
            Title = "Sprint 1",
            DueDate = TestDataBuilder.Future(5),
            Status = MilestoneStatus.NotStarted
        });

        var useCase = new UpdateMilestoneDueDateUseCase(uow);
        var result = await useCase.ExecuteAsync(1, new UpdateMilestoneDueDateRequest(TestDataBuilder.Future(90)));

        result.IsSuccess.Should().BeFalse();
    }
}

public class SkillUseCaseTests
{
    [Fact]
    public async Task AddSkill_Succeeds_ForActiveEmployee()
    {
        var uow = new InMemoryUnitOfWork();
        var employee = TestDataBuilder.CreateUser(2, UserRole.Employee, managerId: 1);
        uow.Users.Seed(employee);
        uow.Employees.Seed(employee.Employee!);

        var useCase = new AddSkillUseCase(uow);
        var result = await useCase.ExecuteAsync(2, new AddSkillRequest("Java", "Backend", "Intermediate"));

        result.IsSuccess.Should().BeTrue();
        uow.EmployeeSkills.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveSkill_Succeeds_WhenSkillExists()
    {
        var uow = new InMemoryUnitOfWork();
        uow.EmployeeSkills.Seed(new EmployeeSkill
        {
            Id = 1,
            EmployeeId = 2,
            SkillName = "Java",
            Category = SkillCategory.Backend,
            Proficiency = ProficiencyLevel.Intermediate
        });

        var useCase = new RemoveSkillUseCase(uow);
        var result = await useCase.ExecuteAsync(1);

        result.IsSuccess.Should().BeTrue();
        uow.EmployeeSkills.Items.Should().BeEmpty();
    }
}

public class GetAllAllocationsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsAllocationsFromQueryService()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetAllocationsAsync(null, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var useCase = new GetAllAllocationsUseCase(query.Object);
        var result = await useCase.ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalActiveCount.Should().Be(0);
    }
}
