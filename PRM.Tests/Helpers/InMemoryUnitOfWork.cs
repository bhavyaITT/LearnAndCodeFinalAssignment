using PRM.Application.Interfaces.Repository;
using PRM.Domain.Entities;

namespace PRM.Tests.Helpers;

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public InMemoryRepository<User> Users { get; } = new();
    public InMemoryRepository<Employee> Employees { get; } = new();
    public InMemoryRepository<EmployeeSkill> EmployeeSkills { get; } = new();
    public InMemoryRepository<Project> Projects { get; } = new();
    public InMemoryRepository<Milestone> Milestones { get; } = new();
    public InMemoryRepository<Allocation> Allocations { get; } = new();
    public InMemoryRepository<Timesheet> Timesheets { get; } = new();
    public InMemoryRepository<TimesheetEntry> TimesheetEntries { get; } = new();
    public InMemoryRepository<SystemConfiguration> SystemConfigurations { get; } = new();

    IRepository<User> IUnitOfWork.Users => Users;
    IRepository<Employee> IUnitOfWork.Employees => Employees;
    IRepository<EmployeeSkill> IUnitOfWork.EmployeeSkills => EmployeeSkills;
    IRepository<Project> IUnitOfWork.Projects => Projects;
    IRepository<Milestone> IUnitOfWork.Milestones => Milestones;
    IRepository<Allocation> IUnitOfWork.Allocations => Allocations;
    IRepository<Timesheet> IUnitOfWork.Timesheets => Timesheets;
    IRepository<TimesheetEntry> IUnitOfWork.TimesheetEntries => TimesheetEntries;
    IRepository<SystemConfiguration> IUnitOfWork.SystemConfigurations => SystemConfigurations;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var user in Users.Items.Where(u => u.Employee is not null))
        {
            user.Employee.Id = user.Id;
            user.Employee.User = user;

            if (!Employees.Items.Any(e => e.Id == user.Id))
                Employees.Seed(user.Employee);
        }

        return Task.FromResult(1);
    }

    public void Dispose() { }
}
