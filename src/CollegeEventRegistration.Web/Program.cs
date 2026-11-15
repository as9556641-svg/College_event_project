using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Hubs;
using CollegeEventRegistration.Web.Services;

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

// Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

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

app.UseRouting();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapHub<NotificationHub>("/hubs/notifications");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
