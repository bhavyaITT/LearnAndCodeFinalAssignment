using global::PRM.Domain.Entities;
using global::PRM.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace PRM.Infrastructure.Persistence
{
    
    /// <summary>
    /// Seeds the first Admin account and default system configuration.
    /// Runs once at application startup. If the data already exists, it skips safely.
    /// Default Admin credentials: admin / Admin@1234 (must be changed on first login).
    /// </summary>
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(PRMTDbContext context)
        {
            await context.Database.MigrateAsync();
            await EnsureTimesheetComplianceColumnsAsync(context);

            await SeedAdminUserAsync(context);
            await SeedSystemConfigurationAsync(context);
        }

        /// <summary>
        /// Applies User compliance columns if the migration was not recorded/applied yet.
        /// </summary>
        private static async Task EnsureTimesheetComplianceColumnsAsync(PRMTDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH('Users', 'TimesheetSubmissionFrozen') IS NULL
                BEGIN
                    ALTER TABLE Users ADD TimesheetSubmissionFrozen bit NOT NULL
                        CONSTRAINT DF_Users_TimesheetSubmissionFrozen DEFAULT 0;
                    ALTER TABLE Users ADD TimesheetReminderCount int NOT NULL
                        CONSTRAINT DF_Users_TimesheetReminderCount DEFAULT 0;
                    ALTER TABLE Users ADD PendingMissedWeekStart date NULL;
                END
                """);

            await context.Database.ExecuteSqlRawAsync("""
                IF NOT EXISTS (
                    SELECT 1 FROM [__EFMigrationsHistory]
                    WHERE [MigrationId] = '20260612100000_AddTimesheetComplianceFields')
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES ('20260612100000_AddTimesheetComplianceFields', '8.0.0');
                """);
        }

        private static async Task SeedAdminUserAsync(PRMTDbContext context)
        {
            if (await context.Users.AnyAsync(u => u.Role == UserRole.Admin))
                return;

            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@intimetec.com",
                FullName = "System Administrator",
                // BCrypt hash of "Admin@1234"
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@1234", 12),
                Role = UserRole.Admin,
                IsActive = true,
                Department = "Administration",
                Designation = "System Admin",
                ForcePasswordChange = true,  // Must change on first login
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            Console.WriteLine("✓ Admin seed account created. Login: admin / Admin@1234");
        }

        private static async Task SeedSystemConfigurationAsync(PRMTDbContext context)
        {
            if (await context.SystemConfigurations.AnyAsync())
                return;

            context.SystemConfigurations.Add(new SystemConfiguration
            {
                LlmProvider = "Gemini",
                LlmApiKey = "",
                SchedulerIntervalHours = 4,
                MaxWeeklyHours = 40,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            Console.WriteLine("✓ Default system configuration seeded.");
        }
    }
}
