using PRM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Domain.Entities
{
    public class User: BaseEntity
    {
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string Department { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool ForcePasswordChange { get; set; } = true;

        /// <summary>When true, employee cannot create or submit timesheets (login/view still allowed).</summary>
        public bool TimesheetSubmissionFrozen { get; set; }

        /// <summary>0 = none, 1 = first reminder sent, 2 = second reminder sent.</summary>
        public int TimesheetReminderCount { get; set; }

        /// <summary>Week start (Monday) for the outstanding missed timesheet being escalated.</summary>
        public DateOnly? PendingMissedWeekStart { get; set; }

        public Employee? Employee { get; set; }
        public ICollection<Employee> ManagedEmployees { get; set; } = [];
    }
}
