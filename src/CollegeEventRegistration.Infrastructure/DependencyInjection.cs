using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Infrastructure.Identity;
using CollegeEventRegistration.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CollegeEventRegistration.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure layer dependencies.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found in configuration.");

        // Configure Entity Framework Core with SQL Server
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            }));

        // Configure ASP.NET Core Identity with ApplicationUser and ApplicationRole
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Password policy
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;

            // Lockout policy
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User configuration
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false; // Enabled later when SMTP is configured
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddUserValidator<SuperAdminUserValidator>()
        .AddDefaultTokenProviders();

        // Register Phase 2.1 Domain & Protection Services
        services.AddScoped<ISuperAdminProtectionService, SuperAdminProtectionService>();
        services.AddScoped<IEventCapacityService, EventCapacityService>();

        // Register Phase 5 Event & Workshop Management Services
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IEventCategoryService, EventCategoryService>();
        services.AddScoped<IBannerUploadService, BannerUploadService>();
        services.AddScoped<IEventManagementService, EventManagementService>();

        // Register Phase 6 Student Event Discovery Services
        services.AddScoped<IEventDiscoveryService, EventDiscoveryService>();

        // Register Phase 7 Event Registration Services
        services.AddScoped<IEventRegistrationService, EventRegistrationService>();

        // Register Phase 8 Payment Integration Services
        services.AddHttpClient<IPaymentGatewayClient, RazorpayGatewayClient>();
        services.AddScoped<IPaymentService, PaymentService>();

        // Register Phase 9 QR Ticket Generation Services
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        services.AddScoped<ITicketService, TicketService>();

        return services;
    }
}

