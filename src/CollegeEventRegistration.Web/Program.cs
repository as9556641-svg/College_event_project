using System.Threading.RateLimiting;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Hubs;
using CollegeEventRegistration.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Register Infrastructure services (DbContext, Identity, SQL Server)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Configure Cookie Authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = ".CollegeEventRegistration.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Configure External Google OAuth Authentication (enabled conditionally when valid credentials exist)
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId.Trim();
            options.ClientSecret = googleClientSecret.Trim();
            options.CallbackPath = "/signin-google";
            options.SaveTokens = true;
        });
}

// Add Rate Limiting for sensitive authentication and public endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AuthPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("GeneralPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 120;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

// Add Health Checks for operational monitoring
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Database");

// Add MVC Controllers and Views with global automatic antiforgery validation
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Real-Time SignalR Notifications
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealTimeNotificationPublisher, SignalRNotificationPublisher>();

var app = builder.Build();

// Seed database, default roles, categories, and Super Admin (if configured)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await DbInitializer.SeedAsync(app.Services, app.Configuration, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database migration or role seeding.");
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Re-execute friendly status code page on client/server status errors (404, 403, etc.)
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

// Global Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(self), microphone=(), geolocation=()");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://checkout.razorpay.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "img-src 'self' data: https://*.googleusercontent.com; " +
        "connect-src 'self' https://api.razorpay.com wss: ws:; " +
        "frame-src 'self' https://api.razorpay.com; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';");

    await next();
});

app.UseRouting();

// Built-in Rate Limiting middleware
app.UseRateLimiter();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Health check endpoint for system uptime & database connectivity checks
app.MapHealthChecks("/health");

app.MapHub<NotificationHub>("/hubs/notifications");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
