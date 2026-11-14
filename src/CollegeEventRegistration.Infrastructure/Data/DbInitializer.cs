using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Data;

/// <summary>
/// Idempotent database seeder for default roles, initial departments, categories, and secure Super Admin provisioning.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Apply any pending migrations automatically on startup
        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            logger.LogInformation("Applying pending database migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }

        // ---------------------------------------------------------------------
        // 1. Seed Roles (Idempotent)
        // ---------------------------------------------------------------------
        var roles = new[]
        {
            new { Name = nameof(UserRole.SuperAdmin), Description = "Root administrator with full institutional governance authority." },
            new { Name = nameof(UserRole.Admin), Description = "Institutional administrator (Deans, HODs) managing events, categories, and approvals." },
            new { Name = nameof(UserRole.Coordinator), Description = "Event coordinator overseeing assigned workshops, rosters, and live QR check-ins." },
            new { Name = nameof(UserRole.Student), Description = "Student or external attendee registering, attending, and receiving certificates." }
        };

        foreach (var roleInfo in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleInfo.Name))
            {
                var role = new ApplicationRole(roleInfo.Name, roleInfo.Description);
                var result = await roleManager.CreateAsync(role);
                if (result.Succeeded)
                {
                    logger.LogInformation("Seeded role: {RoleName}", roleInfo.Name);
                }
                else
                {
                    logger.LogError("Failed to seed role {RoleName}: {Errors}", roleInfo.Name, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        // ---------------------------------------------------------------------
        // 2. Provision Secure Super Admin
        // ---------------------------------------------------------------------
        // Credentials must come from secure configuration, environment variables, or User Secrets.
        var superAdminEmail = configuration["SuperAdmin:Email"]
            ?? Environment.GetEnvironmentVariable("SUPERADMIN_EMAIL");

        var superAdminPassword = configuration["SuperAdmin:Password"]
            ?? Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD");

        var superAdminFirstName = configuration["SuperAdmin:FirstName"] ?? "System";
        var superAdminLastName = configuration["SuperAdmin:LastName"] ?? "Administrator";

        if (!string.IsNullOrWhiteSpace(superAdminEmail) && !string.IsNullOrWhiteSpace(superAdminPassword))
        {
            var existingSuperAdmin = await userManager.FindByEmailAsync(superAdminEmail);
            if (existingSuperAdmin == null)
            {
                var superAdmin = new ApplicationUser
                {
                    UserName = superAdminEmail,
                    Email = superAdminEmail,
                    FirstName = superAdminFirstName,
                    LastName = superAdminLastName,
                    EmailConfirmed = true,
                    IsActive = true,
                    IsSuperAdminProtected = true,
                    Department = "Administration",
                    CollegeName = "Central University"
                };

                var createResult = await userManager.CreateAsync(superAdmin, superAdminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(superAdmin, nameof(UserRole.SuperAdmin));
                    logger.LogInformation("Successfully provisioned initial Super Admin account: {Email}", superAdminEmail);
                }
                else
                {
                    logger.LogError("Failed to create Super Admin account: {Errors}", string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                // Ensure existing root user is marked protected and in SuperAdmin role
                if (!existingSuperAdmin.IsSuperAdminProtected)
                {
                    existingSuperAdmin.IsSuperAdminProtected = true;
                    await userManager.UpdateAsync(existingSuperAdmin);
                }
                if (!await userManager.IsInRoleAsync(existingSuperAdmin, nameof(UserRole.SuperAdmin)))
                {
                    await userManager.AddToRoleAsync(existingSuperAdmin, nameof(UserRole.SuperAdmin));
                }
            }
        }
        else
        {
            logger.LogWarning("Super Admin account was NOT seeded: 'SuperAdmin:Email' and 'SuperAdmin:Password' are not set. " +
                              "Configure them via 'dotnet user-secrets set' or environment variables (SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD).");
        }

        // ---------------------------------------------------------------------
        // 2b. Provision Initial Institutional Admin & Coordinator (Idempotent)
        // ---------------------------------------------------------------------
        var adminEmail = "admin@college.edu";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Institutional",
                LastName = "Administrator",
                EmailConfirmed = true,
                IsActive = true,
                Department = "Administration",
                CollegeName = "Central University"
            };
            var res = await userManager.CreateAsync(adminUser, "Admin@2026!");
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, nameof(UserRole.Admin));
                logger.LogInformation("Provisioned initial Admin user: {Email}", adminEmail);
            }
        }

        var coordEmail = "coordinator@college.edu";
        if (await userManager.FindByEmailAsync(coordEmail) == null)
        {
            var coordUser = new ApplicationUser
            {
                UserName = coordEmail,
                Email = coordEmail,
                FirstName = "Event",
                LastName = "Coordinator",
                EmailConfirmed = true,
                IsActive = true,
                Department = "Computer Science & Engineering",
                CollegeName = "Central University"
            };
            var res = await userManager.CreateAsync(coordUser, "Coordinator@2026!");
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(coordUser, nameof(UserRole.Coordinator));
                logger.LogInformation("Provisioned initial Coordinator user: {Email}", coordEmail);
            }
        }

        // ---------------------------------------------------------------------
        // 3. Seed Default Departments (If table is empty)
        // ---------------------------------------------------------------------
        if (!await context.Departments.AnyAsync())
        {
            var departments = new[]
            {
                new Department { Name = "Computer Science & Engineering", Code = "CSE", Description = "Department of Computer Science & Engineering", HeadOfDepartment = "Dr. C. Sharma" },
                new Department { Name = "Information Technology", Code = "IT", Description = "Department of Information Technology", HeadOfDepartment = "Dr. R. Verma" },
                new Department { Name = "Electronics & Communication Engineering", Code = "ECE", Description = "Department of Electronics & Communication", HeadOfDepartment = "Dr. S. Nair" },
                new Department { Name = "Mechanical Engineering", Code = "MECH", Description = "Department of Mechanical Engineering", HeadOfDepartment = "Dr. A. Patel" },
                new Department { Name = "Management Studies (MBA)", Code = "MBA", Description = "School of Management Studies", HeadOfDepartment = "Dr. M. Iyer" }
            };

            await context.Departments.AddRangeAsync(departments);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded default departments.");
        }

        // ---------------------------------------------------------------------
        // 4. Seed Default Event Categories (If table is empty)
        // ---------------------------------------------------------------------
        if (!await context.EventCategories.AnyAsync())
        {
            var categories = new[]
            {
                new EventCategory { Name = "Technical Workshop", Slug = "technical-workshop", Description = "Hands-on coding, hardware, and engineering workshops.", IconClass = "bi-code-slash" },
                new EventCategory { Name = "Hackathon & Contest", Slug = "hackathon-contest", Description = "Coding hackathons, design sprints, and competitive events.", IconClass = "bi-trophy" },
                new EventCategory { Name = "Guest Lecture & Seminar", Slug = "seminar-guest-lecture", Description = "Distinguished lectures and research talks.", IconClass = "bi-mic" },
                new EventCategory { Name = "National Symposium", Slug = "national-symposium", Description = "Annual technical and academic symposiums.", IconClass = "bi-diagram-3" },
                new EventCategory { Name = "Cultural & Arts", Slug = "cultural-arts", Description = "Music, dance, theatre, and arts festivals.", IconClass = "bi-palette" }
            };

            await context.EventCategories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded default event categories.");
        }
    }
}

