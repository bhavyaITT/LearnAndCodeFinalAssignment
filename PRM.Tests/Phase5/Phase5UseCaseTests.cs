using PRM.Application.DTOs;
using PRM.Application.Interfaces.Service;
using PRM.Application.UseCases.AI;
using PRM.Infrastructure.AI.Prompts;
using PRM.Tests.Helpers;

namespace PRM.Tests.Phase5;

public class SkillMatchUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsRankedCandidates_WhenAiResponds()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetEmployeesForSkillMatchAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new EmployeeAiContext(2, "Hari", "Development", "SE", 50, 20,
                    ["Java"], ["Microservices"])
            ]);

        var llm = new Mock<ILlmClient>();
        llm.Setup(l => l.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                [
                  { "employeeId": 2, "reason": "Strong Java skills on profile.", "suggestedAllocationPercent": null }
                ]
                """);

        var factory = new Mock<ILlmClientFactory>();
        factory.Setup(f => f.GetClientAsync(It.IsAny<CancellationToken>())).ReturnsAsync(llm.Object);

        var useCase = new SkillMatchUseCase(query.Object, factory.Object);
        var result = await useCase.ExecuteAsync(
            new SkillMatchRequest("Java developer with microservices experience"), 1);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Candidates.Should().HaveCount(1);
        result.Data.Candidates.First().FullName.Should().Be("Hari");
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenRequirementEmpty()
    {
        var useCase = new SkillMatchUseCase(Mock.Of<IQueryService>(), Mock.Of<ILlmClientFactory>());
        var result = await useCase.ExecuteAsync(new SkillMatchRequest(""), 1);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenNoCandidatesHaveCapacity()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetEmployeesForSkillMatchAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new EmployeeAiContext(2, "Hari", "Development", "SE", 0, 0, ["Java"], [])
            ]);

        var useCase = new SkillMatchUseCase(query.Object, Mock.Of<ILlmClientFactory>());
        var result = await useCase.ExecuteAsync(
            new SkillMatchRequest("Java developer"), 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("free capacity");
    }
}

public class TeamSkillMatchUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsBenchMatchAndAvailabilityGaps()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetBenchEmployeesForTeamStaffingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new BenchEmployeeAiContext(6, "Yugansh", "Testing", "SE",
                    [new BenchSkillContext("Testing", "QA", "Intermediate")])
            ]);
        query.Setup(q => q.GetAllocatedEmployeesForTeamStaffingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AllocatedEmployeeAiContext(
                    4, "Hari", "Development", "SE",
                    [new BenchSkillContext("Java", "BACKEND", "Beginner")],
                    [new ActiveAllocationContext("Pharos", TestDataBuilder.Past(7), TestDataBuilder.Future(30), 50)])
            ]);

        var llm = new Mock<ILlmClient>();
        llm.Setup(l => l.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {
                  "matches": [
                    { "roleTitle": "QA Tester", "employeeId": 6, "reason": "Yugansh has Testing skills on bench." }
                  ],
                  "gaps": [
                    {
                      "roleTitle": "Java Developer",
                      "gapType": "AvailabilityGap",
                      "reason": "Hari has Java skills but is allocated.",
                      "allocatedEmployeeId": 4,
                      "allocatedEmployeeName": "Hari"
                    }
                  ]
                }
                """);

        var factory = new Mock<ILlmClientFactory>();
        factory.Setup(f => f.GetClientAsync(It.IsAny<CancellationToken>())).ReturnsAsync(llm.Object);

        var useCase = new TeamSkillMatchUseCase(query.Object, factory.Object);
        var result = await useCase.ExecuteAsync(new TeamStaffingRequest(
            "Need a Java developer and a tester for 10 weeks", "Banking portal"));

        result.IsSuccess.Should().BeTrue();
        result.Data!.Matches.Should().HaveCount(1);
        result.Data.Gaps.Should().Contain(g => g.GapType == "AvailabilityGap");
    }

    [Fact]
    public async Task ExecuteAsync_ParsesStringEmployeeIds_FromAiResponse()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetBenchEmployeesForTeamStaffingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new BenchEmployeeAiContext(6, "Yugansh", "Testing", "SE", [])
            ]);
        query.Setup(q => q.GetAllocatedEmployeesForTeamStaffingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var llm = new Mock<ILlmClient>();
        llm.Setup(l => l.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {
                  "matches": [
                    { "roleTitle": "Tester", "employeeId": "6", "reason": "Bench tester." }
                  ],
                  "gaps": []
                }
                """);

        var factory = new Mock<ILlmClientFactory>();
        factory.Setup(f => f.GetClientAsync(It.IsAny<CancellationToken>())).ReturnsAsync(llm.Object);

        var useCase = new TeamSkillMatchUseCase(query.Object, factory.Object);
        var result = await useCase.ExecuteAsync(new TeamStaffingRequest("Need a tester"));

        result.IsSuccess.Should().BeTrue();
        result.Data!.Matches.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenAiReturnsUnreadableJson()
    {
        var query = new Mock<IQueryService>();
        query.Setup(q => q.GetBenchEmployeesForTeamStaffingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BenchEmployeeAiContext(1, "A", "Development", "SE", [])]);
        query.Setup(q => q.GetAllocatedEmployeesForTeamStaffingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var llm = new Mock<ILlmClient>();
        llm.Setup(l => l.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is not JSON");

        var factory = new Mock<ILlmClientFactory>();
        factory.Setup(f => f.GetClientAsync(It.IsAny<CancellationToken>())).ReturnsAsync(llm.Object);

        var useCase = new TeamSkillMatchUseCase(query.Object, factory.Object);
        var result = await useCase.ExecuteAsync(new TeamStaffingRequest("Need a team"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("unreadable");
    }
}

public class SkillMatchPromptBuilderTests
{
    [Fact]
    public void BuildUserPrompt_IncludesFreeHoursForCandidates()
    {
        var prompt = SkillMatchPromptBuilder.BuildUserPrompt(
            "Java developer",
            "Alpha Portal",
            [new EmployeeAiContext(2, "Hari", "Development", "SE", 50, 20, ["Java"], ["API"])]);

        prompt.Should().Contain("20 hrs/week free");
        prompt.Should().Contain("Alpha Portal");
    }
}

public class TeamSkillMatchPromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_UsesValidJsonExample()
    {
        var prompt = TeamSkillMatchPromptBuilder.BuildSystemPrompt();

        prompt.Should().Contain("\"gapType\": \"AvailabilityGap\"");
        prompt.Should().NotContain("SkillGap\" | \"AvailabilityGap");
    }
}
