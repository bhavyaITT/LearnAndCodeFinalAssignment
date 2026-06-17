using Microsoft.EntityFrameworkCore;
using PRM.Application.Common;
using PRM.Application.DTOs.Allocation;
using PRM.Application.DTOs.Employee;
using PRM.Application.DTOs.Manager;
using PRM.Application.DTOs.Project;
using PRM.Application.Interfaces.Service;
using PRM.Domain.Enums;
using PRM.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Infrastructure.ExternalService
{
    /// <summary>
    /// Implements IQueryService using EF Core Include/Join queries.
    /// Lives in Infrastructure — this is the only layer that knows about EF Core.
    /// </summary>
    public class EfQueryService(PRMTDbContext context) : IQueryService
    {
        // ── Phase 2 ───────────────────────────────────────────────

        public async Task<EmployeeDetailResponse?> GetEmployeeWithSkillsAsync(int employeeId, CancellationToken ct = default)
        {
            var employee = await context.Employees
                .Include(e => e.User)
                .Include(e => e.Skills)
                .FirstOrDefaultAsync(e => e.Id == employeeId, ct);

            if (employee is null) return null;

            return new EmployeeDetailResponse(
                employee.Id, employee.User.FullName, employee.User.Email,
                employee.User.Department, employee.User.Designation,
                employee.User.Role.ToString(),
                employee.Status.ToString(), employee.User.IsActive,
                employee.Skills.Select(s => new SkillResponse(
                    s.Id, s.SkillName, s.Category.ToString(), s.Proficiency.ToString()
                ))
            );
        }

        public async Task<ProjectDetailResponse?> GetProjectWithMilestonesAsync(int projectId, CancellationToken ct = default)
        {
            var project = await context.Projects
                .Include(p => p.Manager)
                    .ThenInclude(m => m.User)
                .Include(p => p.Milestones.OrderBy(m => m.DueDate))
                .FirstOrDefaultAsync(p => p.Id == projectId, ct);

            if (project is null) return null;

            return new ProjectDetailResponse(
                project.Id, project.Name, project.Description,
                project.StartDate, project.EndDate,
                project.Status.ToString(), project.Health.ToString(),
                project.ManagerId, project.Manager.User.FullName,
                project.Milestones.Select(m => new MilestoneResponse(
                    m.Id, m.Title, m.DueDate, m.Status.ToString()
                ))
            );
        }

        public async Task<IEnumerable<ProjectSummaryResponse>> GetAllProjectsWithManagerAsync(CancellationToken ct = default)
        {
            return await context.Projects
                .Include(p => p.Manager)
                    .ThenInclude(m => m.User)
                .OrderBy(p => p.Name)
                .Select(p => new ProjectSummaryResponse(
                    p.Id, p.Name, p.Description, p.StartDate, p.EndDate,
                    p.Status.ToString(), p.Health.ToString(),
                    p.ManagerId, p.Manager.User.FullName
                ))
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<AllocationResponse>> GetAllocationsAsync(
            int? employeeId, int? projectId, bool activeOnly, CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var query = context.Allocations
                .Include(a => a.Employee)
                    .ThenInclude(e => e.User)
                .Include(a => a.Project)
                .AsQueryable();

            if (activeOnly)
                query = query.Where(a => a.ToDate >= today);

            if (employeeId.HasValue)
                query = query.Where(a => a.EmployeeId == employeeId.Value);

            if (projectId.HasValue)
                query = query.Where(a => a.ProjectId == projectId.Value);

            return await query
                .OrderBy(a => a.Employee.User.FullName)
                .ThenBy(a => a.Project.Name)
                .Select(a => new AllocationResponse(
                    a.Id, a.EmployeeId, a.Employee.User.FullName,
                    a.ProjectId, a.Project.Name,
                    a.UtilisationPercentage, a.FromDate, a.ToDate
                ))
                .ToListAsync(ct);
        }

        // ── Phase 3 — Resource Dashboard ─────────────────────────

        public async Task<IEnumerable<EmployeeUtilisationRow>> GetActiveEmployeeUtilisationsAsync(CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // For each active employee, sum utilisation of overlapping allocations
            var rows = await context.Employees
                .Where(e => e.User.IsActive)
                .Select(e => new EmployeeUtilisationRow(
                    e.Id,
                    e.User.FullName,
                    e.User.Department,  
                    e.User.Designation,
                    e.Allocations
                        .Where(a => a.FromDate <= today && a.ToDate >= today)
                        .Sum(a => a.UtilisationPercentage)
                ))
                .ToListAsync(ct);

            return rows;
        }

        public async Task<IEnumerable<TeamEmployeeDashboardRow>> GetTeamEmployeesForDashboardAsync(
            int managerEmployeeId, CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            return await context.Employees
                .Where(e => e.User.IsActive
                         && e.User.Role == UserRole.Employee
                         && e.ManagerId == managerEmployeeId
                         && e.Id != managerEmployeeId)
                .OrderBy(e => e.User.FullName)
                .Select(e => new TeamEmployeeDashboardRow(
                    e.Id,
                    e.User.FullName,
                    e.User.Department,
                    e.User.Designation,
                    e.Allocations.Any(a => a.FromDate <= today && a.ToDate >= today)
                        ? "Allocated"
                        : "Bench",
                    e.Allocations
                        .Where(a => a.FromDate <= today && a.ToDate >= today)
                        .Sum(a => a.UtilisationPercentage)
                ))
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<TeamEmployeeAllocationRow>> GetTeamEmployeeAllocationRowsAsync(
            int managerEmployeeId, CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var team = await context.Employees
                .Where(e => e.User.IsActive
                         && e.User.Role == UserRole.Employee
                         && e.ManagerId == managerEmployeeId
                         && e.Id != managerEmployeeId)
                .Include(e => e.User)
                .Include(e => e.Allocations.Where(a => a.ToDate >= today && a.ToDate >= a.FromDate))
                    .ThenInclude(a => a.Project)
                .OrderBy(e => e.User.FullName)
                .ToListAsync(ct);

            var rows = new List<TeamEmployeeAllocationRow>();

            foreach (var employee in team)
            {
                var allocations = employee.Allocations
                    .Where(a => AllocationDateHelper.IsEndable(a, today))
                    .OrderBy(a => a.FromDate)
                    .ToList();

                if (allocations.Count == 0)
                {
                    rows.Add(new TeamEmployeeAllocationRow(
                        employee.Id,
                        employee.User.FullName,
                        "-",
                        0,
                        today,
                        today,
                        false));
                    continue;
                }

                foreach (var allocation in allocations)
                {
                    rows.Add(new TeamEmployeeAllocationRow(
                        employee.Id,
                        employee.User.FullName,
                        allocation.Project.Name,
                        allocation.UtilisationPercentage,
                        allocation.FromDate,
                        allocation.ToDate,
                        AllocationDateHelper.IsActiveOnDate(allocation, today)));
                }
            }

            return rows;
        }

        public async Task<bool> IsEmployeeUnderManagerAsync(
            int employeeId, int managerEmployeeId, CancellationToken ct = default)
        {
            return await context.Employees
                .AnyAsync(e => e.Id == employeeId
                            && e.ManagerId == managerEmployeeId
                            && e.Id != managerEmployeeId
                            && e.User.IsActive
                            && e.User.Role == UserRole.Employee, ct);
        }

        public async Task<IEnumerable<string>> GetRecentActivityTagsAsync(int employeeId, int weekCount, CancellationToken ct = default)
        {
            var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7 * weekCount));

            var tagStrings = await context.TimesheetEntries
                .Where(te => te.Timesheet.EmployeeId == employeeId
                          && te.Timesheet.WeekStartDate >= cutoff
                          && !string.IsNullOrEmpty(te.ActivityTags))
                .Select(te => te.ActivityTags)
                .ToListAsync(ct);

            // Tags are stored comma-separated — split, trim, deduplicate
            return tagStrings
                .SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        public async Task<IEnumerable<ActiveAllocationItem>> GetActiveAllocationsForEmployeeAsync(int employeeId, CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            return await context.Allocations
                .Include(a => a.Project)
                .Where(a => a.EmployeeId == employeeId
                         && a.FromDate <= today
                         && a.ToDate >= today)
                .OrderBy(a => a.Project.Name)
                .Select(a => new ActiveAllocationItem(
                    a.Id, a.Project.Name, a.UtilisationPercentage, a.FromDate, a.ToDate
                ))
                .ToListAsync(ct);
        }

        //// ── Phase 3 — Manager Projects ────────────────────────────

        public async Task<IEnumerable<ManagerProjectSummaryResponse>> GetManagerProjectsAsync(int managerEmployeeId, CancellationToken ct = default)
        {
            return await context.Projects
                .Where(p => p.ManagerId == managerEmployeeId)
                .OrderBy(p => p.EndDate)
                .Select(p => new ManagerProjectSummaryResponse(p.Id, p.Name, p.EndDate, p.Health.ToString()))
                .ToListAsync(ct);
        }

        public async Task<ManagerProjectDetailResponse?> GetManagerProjectDetailAsync(int projectId, int managerEmployeeId, CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var project = await context.Projects
                .Include(p => p.Milestones.OrderBy(m => m.DueDate))
                .Include(p => p.Allocations.Where(a => a.ToDate >= today && a.ToDate >= a.FromDate))
                    .ThenInclude(a => a.Employee)
                        .ThenInclude(e => e.User)
                .FirstOrDefaultAsync(p => p.Id == projectId && p.ManagerId == managerEmployeeId, ct);

            if (project is null) return null;

            var flags = await BuildRiskFlagsAsync(project, today, ct);

            var milestones = project.Milestones.Select(m => new MilestoneResponse(
                m.Id, m.Title, m.DueDate, m.Status.ToString()
            ));

            var resources = project.Allocations.Select(a => new AllocationOnProjectResponse(
                a.Id, a.EmployeeId, a.Employee.User.FullName, a.UtilisationPercentage, a.FromDate, a.ToDate
            ));

            return new ManagerProjectDetailResponse(
                project.Id, project.Name, project.StartDate, project.EndDate,
                project.Status.ToString(), project.Health.ToString(),
                flags, milestones, resources
            );
        }

        private async Task<IEnumerable<RiskFlagItem>> BuildRiskFlagsAsync(
            Domain.Entities.Project project,
            DateOnly today,
            CancellationToken ct)
        {
            var flags = new List<RiskFlagItem>();
            var milestoneList = project.Milestones.ToList();

            var overdue = milestoneList
                .Where(m => m.Status != MilestoneStatus.Done && m.DueDate < today)
                .ToList();

            foreach (var m in overdue)
            {
                var days = today.DayNumber - m.DueDate.DayNumber;
                flags.Add(new RiskFlagItem(true, $"{m.Title} milestone is {days} day(s) overdue"));
            }

            var lastMonday = ProjectHealthCalculator.GetLastCompletedWeekMonday(today);
            var lastSunday = lastMonday.AddDays(6);

            var allocatedDuringWeek = await context.Allocations
                .Include(a => a.Employee)
                    .ThenInclude(e => e.User)
                .Where(a => a.ProjectId == project.Id
                         && a.FromDate <= lastSunday
                         && a.ToDate >= lastMonday)
                .Select(a => new { a.EmployeeId, a.Employee.User.FullName })
                .Distinct()
                .ToListAsync(ct);

            foreach (var employee in allocatedDuringWeek)
            {
                var submitted = await context.Timesheets.AnyAsync(
                    t => t.EmployeeId == employee.EmployeeId
                      && t.WeekStartDate == lastMonday
                      && t.Status == TimesheetStatus.Submitted,
                    ct);

                if (!submitted)
                {
                    flags.Add(new RiskFlagItem(
                        true,
                        $"{employee.FullName} did not submit timesheet for week {lastMonday:dd-MMM-yyyy}"));
                }
            }

            if (!flags.Any(f => f.IsCritical))
                flags.Add(new RiskFlagItem(false, "All milestones are on track"));

            if (allocatedDuringWeek.Count > 0
                && !flags.Any(f => f.IsCritical && f.Message.Contains("timesheet", StringComparison.OrdinalIgnoreCase)))
            {
                flags.Add(new RiskFlagItem(
                    false,
                    "All allocated employees submitted timesheets for the last completed week"));
            }

            var hasNotStarted = milestoneList.Any(m => m.Status == MilestoneStatus.NotStarted);
            var hasInProgress = milestoneList.Any(m => m.Status == MilestoneStatus.InProgress);

            if (!hasNotStarted && !hasInProgress && milestoneList.Count > 0)
                flags.Add(new RiskFlagItem(false, "All milestones completed"));

            return flags;
        }

        //// ── Phase 3 — Team Timesheets ─────────────────────────────

        public async Task<TeamTimesheetResponse> GetTeamTimesheetsAsync(int managerEmployeeId, DateOnly weekStart, CancellationToken ct = default)
        {
            var weekEnd = weekStart.AddDays(6);

            var teamEmployees = await context.Employees
                .Include(e => e.User)
                .Where(e => e.ManagerId == managerEmployeeId
                         && e.Id != managerEmployeeId
                         && e.User.IsActive
                         && e.User.Role == UserRole.Employee)
                .OrderBy(e => e.User.FullName)
                .ToListAsync(ct);

            var entries = new List<TeamTimesheetEntryResponse>();

            foreach (var employee in teamEmployees)
            {
                var timesheet = await context.Timesheets
                    .Include(t => t.Entries)
                        .ThenInclude(e => e.Project)
                    .FirstOrDefaultAsync(t => t.EmployeeId == employee.Id && t.WeekStartDate == weekStart, ct);

                if (timesheet is not null)
                {
                    if (timesheet.Status == TimesheetStatus.Missed)
                    {
                        entries.Add(new TeamTimesheetEntryResponse(
                            employee.Id, employee.User.FullName,
                            0, "-", 0, TimesheetStatus.Missed.ToString()
                        ));
                        continue;
                    }

                    if (timesheet.Entries.Count == 0)
                    {
                        entries.Add(new TeamTimesheetEntryResponse(
                            employee.Id, employee.User.FullName,
                            0, "-", 0, TimesheetStatus.Submitted.ToString()
                        ));
                        continue;
                    }

                    foreach (var entry in timesheet.Entries)
                    {
                        entries.Add(new TeamTimesheetEntryResponse(
                            employee.Id, employee.User.FullName,
                            entry.ProjectId, entry.Project.Name,
                            entry.HoursWorked, timesheet.Status.ToString()
                        ));
                    }

                    continue;
                }

                var hadAllocation = await context.Allocations.AnyAsync(
                    a => a.EmployeeId == employee.Id
                      && a.FromDate <= weekEnd
                      && a.ToDate >= weekStart, ct);

                entries.Add(new TeamTimesheetEntryResponse(
                    employee.Id, employee.User.FullName,
                    0, hadAllocation ? "-" : "N/A",
                    0, hadAllocation ? TimesheetStatus.Pending.ToString() : "No allocation"
                ));
            }

            return new TeamTimesheetResponse(weekStart, entries);
        }

        public async Task<TeamTimesheetDetailResponse?> GetTeamMemberTimesheetDetailAsync(int employeeId, DateOnly weekStart, CancellationToken ct = default)
        {
            var timesheet = await context.Timesheets
                .Include(t => t.Employee)
                    .ThenInclude(e => e.User)
                .Include(t => t.Entries)
                    .ThenInclude(e => e.Project)
                .FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.WeekStartDate == weekStart, ct);

            if (timesheet is null) return null;

            var projectEntries = timesheet.Entries.Select(e => new TeamTimesheetProjectEntryResponse(
                e.Project.Name,
                e.HoursWorked,
                e.ActivityTags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList()
            ));

            return new TeamTimesheetDetailResponse(
                employeeId, timesheet.Employee.User.FullName,
                weekStart, timesheet.TotalHours,
                timesheet.Status.ToString(), projectEntries
            );
        }

        //// ── Phase 3 — Allocation helpers ─────────────────────────

        public async Task<IEnumerable<AllocationOnProjectResponse>> GetActiveAllocationsOnProjectAsync(int projectId, CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            return await context.Allocations
                .Include(a => a.Employee)
                    .ThenInclude(e => e.User)
                .Where(a => a.ProjectId == projectId
                         && a.ToDate >= today
                         && a.ToDate >= a.FromDate)
                .OrderBy(a => a.Employee.User.FullName)
                .Select(a => new AllocationOnProjectResponse(
                    a.Id, a.EmployeeId, a.Employee.User.FullName,
                    a.UtilisationPercentage, a.FromDate, a.ToDate
                ))
                .ToListAsync(ct);
        }

        public async Task<int> GetEmployeeUtilisationInPeriodAsync(
            int employeeId, DateOnly from, DateOnly to,
            int? excludeAllocationId, CancellationToken ct = default)
        {
            // Two date ranges overlap when: fromA <= toB AND toA >= fromB
            var query = context.Allocations
                .Where(a => a.EmployeeId == employeeId
                         && a.FromDate <= to
                         && a.ToDate >= from);

            if (excludeAllocationId.HasValue)
                query = query.Where(a => a.Id != excludeAllocationId.Value);

            return await query.SumAsync(a => a.UtilisationPercentage, ct);
        }

        public async Task<bool> HasOverlappingAllocationOnProjectAsync(
            int employeeId, int projectId, DateOnly from, DateOnly to,
            int? excludeAllocationId, CancellationToken ct = default)
        {
            var query = context.Allocations
                .Where(a => a.EmployeeId == employeeId
                         && a.ProjectId == projectId
                         && a.FromDate <= to
                         && a.ToDate >= from);

            if (excludeAllocationId.HasValue)
                query = query.Where(a => a.Id != excludeAllocationId.Value);

            return await query.AnyAsync(ct);
        }

        //// ── Phase 4 — Employee Timesheets ─────────────────────────

        public async Task<IEnumerable<ActiveAllocationForSubmitResponse>> GetActiveAllocationsForWeekAsync(
            int employeeId, DateOnly weekStart, int maxWeeklyHours, CancellationToken ct = default)
        {
            // A week runs Monday–Sunday. An allocation covers this week if:
            // allocation.FromDate <= weekEnd AND allocation.ToDate >= weekStart
            var weekEnd = weekStart.AddDays(6);

            return await context.Allocations
                .Include(a => a.Project)
                .Where(a => a.EmployeeId == employeeId
                         && a.FromDate <= weekEnd
                         && a.ToDate >= weekStart)
                .OrderBy(a => a.Project.Name)
                .Select(a => new ActiveAllocationForSubmitResponse(
                    a.ProjectId,
                    a.Project.Name,
                    a.UtilisationPercentage,
                    // Per-project hour cap = allocation% × max weekly hours
                    a.UtilisationPercentage * maxWeeklyHours / 100
                ))
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<TimesheetSummaryResponse>> GetMyTimesheetsAsync(
            int employeeId, CancellationToken ct = default)
        {
            return await context.Timesheets
                .Where(t => t.EmployeeId == employeeId)
                .OrderByDescending(t => t.WeekStartDate)
                .Select(t => new TimesheetSummaryResponse(
                    t.Id,
                    t.WeekStartDate,
                    t.TotalHours,
                    t.Status.ToString()
                ))
                .ToListAsync(ct);
        }

        public async Task<TimesheetDetailResponse?> GetMyTimesheetDetailAsync(
            int employeeId, DateOnly weekStart, CancellationToken ct = default)
        {
            var timesheet = await context.Timesheets
                .Include(t => t.Entries)
                    .ThenInclude(e => e.Project)
                .FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.WeekStartDate == weekStart, ct);

            if (timesheet is null) return null;

            var entries = timesheet.Entries.Select(e => new TimesheetEntryResponse(
                e.Id,
                e.ProjectId,
                e.Project.Name,
                e.HoursWorked,
                e.ActivityTags
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList()
            ));

            return new TimesheetDetailResponse(
                timesheet.Id,
                timesheet.WeekStartDate,
                timesheet.TotalHours,
                timesheet.Status.ToString(),
                entries
            );
        }

        public async Task<MyAllocationsResponse> GetMyAllocationsAsync(
            int employeeId, CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var allocations = await context.Allocations
                .Include(a => a.Project)
                .Where(a => a.EmployeeId == employeeId)
                .OrderByDescending(a => a.FromDate)
                .Select(a => new MyAllocationResponse(
                    a.Id,
                    a.ProjectId,
                    a.Project.Name,
                    a.UtilisationPercentage,
                    a.FromDate,
                    a.ToDate,
                    // Active = currently ongoing, Past = already ended
                    a.ToDate >= today ? "Active" : "Past"
                ))
                .ToListAsync(ct);

            var totalActive = allocations
                .Where(a => a.AllocationStatus == "Active")
                .Sum(a => a.UtilisationPercentage);

            return new MyAllocationsResponse(allocations, totalActive);
        }

        //// ── Phase 5 stub ──────────────────────────────────────────

        public async Task<IEnumerable<EmployeeAiContext>> GetEmployeesForSkillMatchAsync(CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var fourWeeksAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-28));

            var employees = await context.Employees
                .Include(e => e.User)
                .Include(e => e.Skills)
                .Include(e => e.Allocations.Where(a => a.FromDate <= today && a.ToDate >= today))
                .Include(e => e.Timesheets.Where(t => t.WeekStartDate >= fourWeeksAgo))
                    .ThenInclude(t => t.Entries)
                .Where(e => e.User.IsActive)
                .ToListAsync(ct);

            return employees.Select(e =>
            {
                var totalUtil = e.Allocations.Sum(a => a.UtilisationPercentage);
                var recentTags = e.Timesheets
                    .SelectMany(t => t.Entries)
                    .Where(en => !string.IsNullOrEmpty(en.ActivityTags))
                    .SelectMany(en => en.ActivityTags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .Select(t => t.Trim())
                    .Distinct()
                    .ToList();

                return new EmployeeAiContext(
                    e.Id, e.User.FullName, e.User.Department, e.User.Designation,
                    Math.Max(0, 100 - totalUtil),
                    e.Skills.Select(s => s.SkillName).ToList(),
                    recentTags
                );
            });
        }

        public async Task<IEnumerable<BenchEmployeeAiContext>> GetBenchEmployeesForTeamStaffingAsync(CancellationToken ct = default)
        {
            var employees = await context.Employees
                .AsNoTracking()
                .Include(e => e.User)
                .Include(e => e.Skills)
                .Where(e => e.User.IsActive
                         && e.User.Role == UserRole.Employee
                         && e.Status == EmployeeStatus.Bench)
                .OrderBy(e => e.User.FullName)
                .ToListAsync(ct);

            return employees.Select(e => new BenchEmployeeAiContext(
                e.Id,
                e.User.FullName,
                e.User.Department,
                e.User.Designation,
                e.Skills.Select(s => new BenchSkillContext(
                    s.SkillName, s.Category.ToString(), s.Proficiency.ToString()))
            ));
        }

        public async Task<IEnumerable<AllocatedEmployeeAiContext>> GetAllocatedEmployeesForTeamStaffingAsync(
            CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var employees = await context.Employees
                .AsNoTracking()
                .Include(e => e.User)
                .Include(e => e.Skills)
                .Include(e => e.Allocations)
                    .ThenInclude(a => a.Project)
                .Where(e => e.User.IsActive
                         && e.User.Role == UserRole.Employee
                         && e.Status == EmployeeStatus.Allocated)
                .OrderBy(e => e.User.FullName)
                .ToListAsync(ct);

            return employees.Select(e => new AllocatedEmployeeAiContext(
                e.Id,
                e.User.FullName,
                e.User.Department,
                e.User.Designation,
                e.Skills.Select(s => new BenchSkillContext(
                    s.SkillName, s.Category.ToString(), s.Proficiency.ToString())),
                e.Allocations
                    .Where(a => a.ToDate >= today)
                    .OrderBy(a => a.FromDate)
                    .Select(a => new ActiveAllocationContext(
                        a.Project.Name,
                        a.FromDate,
                        a.ToDate,
                        a.UtilisationPercentage))
            ));
        }

        public async Task<IEnumerable<SkillAvailabilityHint>> GetSkillAvailabilityHintsAsync(
            IEnumerable<string> skillNames, CancellationToken ct = default)
        {
            var names = skillNames
                .Select(n => n.Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
                return [];

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var skills = (await context.EmployeeSkills
                .AsNoTracking()
                .Include(s => s.Employee)
                    .ThenInclude(e => e.User)
                .Include(s => s.Employee)
                    .ThenInclude(e => e.Allocations)
                .Where(s => s.Employee.User.IsActive
                         && s.Employee.User.Role == UserRole.Employee
                         && s.Employee.Status == EmployeeStatus.Allocated)
                .ToListAsync(ct))
                .Where(s => names.Any(n => n.Equals(s.SkillName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return skills
                .GroupBy(s => s.SkillName, StringComparer.OrdinalIgnoreCase)
                .SelectMany(g =>
                {
                    var byEmployee = g.GroupBy(s => s.EmployeeId);
                    return byEmployee.Select(empSkills =>
                    {
                        var employee = empSkills.First().Employee;
                        var latestEnd = employee.Allocations
                            .Where(a => a.ToDate >= today)
                            .Select(a => a.ToDate)
                            .DefaultIfEmpty(today)
                            .Max();

                        return new SkillAvailabilityHint(
                            empSkills.First().SkillName,
                            employee.User.FullName,
                            latestEnd.AddDays(1));
                    });
                })
                .OrderBy(h => h.SkillName)
                .ThenBy(h => h.AvailableFrom);
        }
    }
}
