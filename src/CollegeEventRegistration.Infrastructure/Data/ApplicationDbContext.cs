using CollegeEventRegistration.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Infrastructure.Data;

/// <summary>
/// Entity Framework Core DbContext integrating ASP.NET Core Identity and application entities.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Custom schema configurations for ApplicationUser
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.RollNumber).HasMaxLength(50);
            entity.Property(u => u.CollegeName).HasMaxLength(200);
            entity.Property(u => u.Department).HasMaxLength(100);
        });

        // Custom schema configurations for ApplicationRole
        builder.Entity<ApplicationRole>(entity =>
        {
            entity.Property(r => r.Description).HasMaxLength(250);
        });
    }
}
