using PRM.Domain.Entities;
using PRM.Domain.Enums;

namespace PRM.Tests.Helpers;

public static class TestDataBuilder
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    public static DateOnly Future(int days) => Today.AddDays(days);
    public static DateOnly Past(int days) => Today.AddDays(-days);

    public static User CreateUser(
        int id,
        UserRole role,
        string username = "user",
        bool isActive = true,
        int managerId = 1)
    {
        var user = new User
        {
            Id = id,
            Username = $"{username}{id}",
            FullName = $"User {id}",
            Email = $"user{id}@intimetec.com",
            PasswordHash = "hash",
            Role = role,
            IsActive = isActive,
            Department = "Development",
            Designation = role == UserRole.Manager ? "Manager" : "SE"
        };

        if (role is UserRole.Manager or UserRole.Employee)
        {
            user.Employee = new Employee
            {
                Id = id,
                Status = EmployeeStatus.Bench,
                ManagerId = managerId,
                User = user
            };
        }

        return user;
    }

    public static Project CreateProject(
        int id,
        int managerId,
        ProjectStatus status = ProjectStatus.Active,
        DateOnly? start = null,
        DateOnly? end = null) =>
        new()
        {
            Id = id,
            Name = $"Project {id}",
            Description = "Test project",
            StartDate = start ?? Past(7),
            EndDate = end ?? Future(90),
            Status = status,
            Health = ProjectHealth.OnTrack,
            ManagerId = managerId
        };

    public static Allocation CreateAllocation(
        int id,
        int employeeId,
        int projectId,
        int utilisation,
        DateOnly? from = null,
        DateOnly? to = null) =>
        new()
        {
            Id = id,
            EmployeeId = employeeId,
            ProjectId = projectId,
            UtilisationPercentage = utilisation,
            FromDate = from ?? Past(7),
            ToDate = to ?? Future(30)
        };

    public static SystemConfiguration DefaultConfig() =>
        new()
        {
            Id = 1,
            LlmProvider = "Gemini",
            LlmApiKey = "test-key",
            SchedulerIntervalHours = 24,
            MaxWeeklyHours = 40
        };
}
