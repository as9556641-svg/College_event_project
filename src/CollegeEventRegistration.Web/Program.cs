using CollegeEventRegistration.Infrastructure;
using CollegeEventRegistration.Infrastructure.Data;

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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

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
}

app.UseHttpsRedirection();
app.UseRouting();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
