using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Infrastructure.Data;

/// <summary>
/// Entity Framework Core DbContext integrating ASP.NET Core Identity and all event management entities.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventSession> EventSessions => Set<EventSession>();
    public DbSet<EventCoordinator> EventCoordinators => Set<EventCoordinator>();
    public DbSet<EventEligibility> EventEligibilities => Set<EventEligibility>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<EventFeedback> EventFeedbacks => Set<EventFeedback>();
    public DbSet<CertificateTemplate> CertificateTemplates => Set<CertificateTemplate>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---------------------------------------------------------------------
        // 1. Identity Entity Configurations
        // ---------------------------------------------------------------------
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.RollNumber).HasMaxLength(50);
            entity.Property(u => u.CollegeName).HasMaxLength(200);
            entity.Property(u => u.Department).HasMaxLength(100);

            entity.HasIndex(u => u.RollNumber);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.Property(r => r.Description).HasMaxLength(250);
        });

        // ---------------------------------------------------------------------
        // 2. Department
        // ---------------------------------------------------------------------
        builder.Entity<Department>(entity =>
        {
            entity.Property(d => d.Name).HasMaxLength(150).IsRequired();
            entity.Property(d => d.Code).HasMaxLength(20).IsRequired();
            entity.Property(d => d.Description).HasMaxLength(500);
            entity.Property(d => d.HeadOfDepartment).HasMaxLength(100);

            entity.HasIndex(d => d.Code).IsUnique();
        });

        // ---------------------------------------------------------------------
        // 3. EventCategory
        // ---------------------------------------------------------------------
        builder.Entity<EventCategory>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Slug).HasMaxLength(120).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(500);
            entity.Property(c => c.IconClass).HasMaxLength(50);

            entity.HasIndex(c => c.Slug).IsUnique();
        });

        // ---------------------------------------------------------------------
        // 4. Event
        // ---------------------------------------------------------------------
        builder.Entity<Event>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Events_Capacity", "[RegisteredCount] <= [TotalCapacity]");
                t.HasCheckConstraint("CK_Events_NonNegativeCounts", "[RegisteredCount] >= 0 AND [TotalCapacity] >= 0");
            });

            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(220).IsRequired();
            entity.Property(e => e.ShortDescription).HasMaxLength(300).IsRequired();
            entity.Property(e => e.RegistrationFee).HasPrecision(18, 2);
            entity.Property(e => e.Venue).HasMaxLength(250);
            entity.Property(e => e.OnlineMeetingUrl).HasMaxLength(500);
            entity.Property(e => e.BannerImageUrl).HasMaxLength(500);

            // Optimistic Concurrency Token for safe seat reservations
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.EventStartDate);

            // Relationships
            entity.HasOne(e => e.Department)
                  .WithMany(d => d.Events)
                  .HasForeignKey(e => e.DepartmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EventCategory)
                  .WithMany(c => c.Events)
                  .HasForeignKey(e => e.EventCategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedByUser)
                  .WithMany(u => u.CreatedEvents)
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.RejectionReason).HasMaxLength(500);

            entity.HasOne(e => e.ApprovedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ApprovedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Global soft-delete query filter
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 5. EventSession (Multi-day events support)
        // ---------------------------------------------------------------------
        builder.Entity<EventSession>(entity =>
        {
            entity.Property(s => s.SessionTitle).HasMaxLength(200).IsRequired();
            entity.Property(s => s.SpeakerName).HasMaxLength(100);
            entity.Property(s => s.SpeakerDesignation).HasMaxLength(100);
            entity.Property(s => s.SpeakerOrganization).HasMaxLength(150);
            entity.Property(s => s.VenueOrRoom).HasMaxLength(100);

            entity.HasOne(s => s.Event)
                  .WithMany(e => e.Sessions)
                  .HasForeignKey(s => s.EventId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(s => !s.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 6. EventCoordinator (Many-to-Many junction)
        // ---------------------------------------------------------------------
        builder.Entity<EventCoordinator>(entity =>
        {
            entity.HasIndex(ec => new { ec.EventId, ec.UserId }).IsUnique();

            entity.HasOne(ec => ec.Event)
                  .WithMany(e => e.Coordinators)
                  .HasForeignKey(ec => ec.EventId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ec => ec.User)
                  .WithMany(u => u.CoordinatedEvents)
                  .HasForeignKey(ec => ec.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(ec => !ec.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 7. EventEligibility
        // ---------------------------------------------------------------------
        builder.Entity<EventEligibility>(entity =>
        {
            entity.Property(ee => ee.MinimumCgpa).HasPrecision(3, 2);
            entity.Property(ee => ee.Remarks).HasMaxLength(300);

            entity.HasOne(ee => ee.Event)
                  .WithMany(e => e.Eligibilities)
                  .HasForeignKey(ee => ee.EventId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ee => ee.Department)
                  .WithMany()
                  .HasForeignKey(ee => ee.DepartmentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(ee => !ee.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 8. EventRegistration (Prevents duplicate student registrations)
        // ---------------------------------------------------------------------
        builder.Entity<EventRegistration>(entity =>
        {
            entity.Property(r => r.RegistrationNumber).HasMaxLength(50).IsRequired();
            entity.Property(r => r.CancellationReason).HasMaxLength(300);

            entity.HasIndex(r => r.RegistrationNumber).IsUnique();

            // Unique composite index: Prevents duplicate active registration for the same event & user
            entity.HasIndex(r => new { r.EventId, r.UserId })
                  .IsUnique();

            entity.HasOne(r => r.Event)
                  .WithMany(e => e.Registrations)
                  .HasForeignKey(r => r.EventId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.User)
                  .WithMany(u => u.Registrations)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(r => !r.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 9. Payment (1-to-Many with Registration for retries and attempt history)
        // ---------------------------------------------------------------------
        builder.Entity<Payment>(entity =>
        {
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.Currency).HasMaxLength(10).IsRequired();
            entity.Property(p => p.OrderId).HasMaxLength(100);
            entity.Property(p => p.TransactionId).HasMaxLength(150);
            entity.Property(p => p.PaymentMethod).HasMaxLength(50);
            entity.Property(p => p.FailureReason).HasMaxLength(300);

            // Foreign key index (non-unique to permit multiple payment attempts/retries)
            entity.HasIndex(p => p.RegistrationId, "IX_Payments_RegistrationId");

            // Filtered UNIQUE index: Strictly prevents more than one SUCCESSFUL payment per registration
            entity.HasIndex(p => p.RegistrationId, "UX_Payments_Registration_Success")
                  .IsUnique()
                  .HasFilter($"[Status] = {(int)PaymentStatus.Success}");

            // Filtered index on TransactionId (non-null unique to prevent transaction ID replays)
            entity.HasIndex(p => p.TransactionId, "UX_Payments_TransactionId")
                  .IsUnique()
                  .HasFilter("[TransactionId] IS NOT NULL");

            entity.HasIndex(p => p.OrderId, "IX_Payments_OrderId");

            // 1-to-Many Relationship: One EventRegistration has many Payment attempts
            entity.HasOne(p => p.Registration)
                  .WithMany(r => r.Payments)
                  .HasForeignKey(p => p.RegistrationId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(p => !p.Registration.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 10. Ticket (1-to-1 with Registration)
        // ---------------------------------------------------------------------
        builder.Entity<Ticket>(entity =>
        {
            entity.Property(t => t.TicketCode).HasMaxLength(100).IsRequired();
            entity.Property(t => t.QrCodeData).HasMaxLength(500).IsRequired();
            entity.Property(t => t.QrCodeImagePath).HasMaxLength(500);

            entity.HasIndex(t => t.TicketCode).IsUnique();

            entity.HasOne(t => t.Registration)
                  .WithOne(r => r.Ticket)
                  .HasForeignKey<Ticket>(t => t.RegistrationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(t => !t.Registration.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 11. Attendance (1-to-1 with Registration)
        // ---------------------------------------------------------------------
        builder.Entity<Attendance>(entity =>
        {
            entity.Property(a => a.Remarks).HasMaxLength(300);

            entity.HasOne(a => a.Registration)
                  .WithOne(r => r.Attendance)
                  .HasForeignKey<Attendance>(a => a.RegistrationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.CheckedInByUser)
                  .WithMany(u => u.VerifiedAttendances)
                  .HasForeignKey(a => a.CheckedInByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(a => !a.Registration.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 12. EventFeedback (1-to-1 with Registration)
        // ---------------------------------------------------------------------
        builder.Entity<EventFeedback>(entity =>
        {
            entity.Property(f => f.Comments).HasMaxLength(1000);

            entity.HasIndex(f => f.RegistrationId).IsUnique();

            entity.HasOne(f => f.Registration)
                  .WithOne(r => r.Feedback)
                  .HasForeignKey<EventFeedback>(f => f.RegistrationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.Event)
                  .WithMany(e => e.FeedbackList)
                  .HasForeignKey(f => f.EventId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.User)
                  .WithMany(u => u.SubmittedFeedbacks)
                  .HasForeignKey(f => f.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(f => !f.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 13. CertificateTemplate (1-to-1 with Event)
        // ---------------------------------------------------------------------
        builder.Entity<CertificateTemplate>(entity =>
        {
            entity.Property(ct => ct.TemplateTitle).HasMaxLength(200).IsRequired();
            entity.Property(ct => ct.SignatoryName).HasMaxLength(100).IsRequired();
            entity.Property(ct => ct.SignatoryTitle).HasMaxLength(100).IsRequired();
            entity.Property(ct => ct.SignatureImagePath).HasMaxLength(500);
            entity.Property(ct => ct.BackgroundImagePath).HasMaxLength(500);
            entity.Property(ct => ct.CertificateCodePrefix).HasMaxLength(20);

            entity.HasOne(ct => ct.Event)
                  .WithOne(e => e.CertificateTemplate)
                  .HasForeignKey<CertificateTemplate>(ct => ct.EventId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(ct => !ct.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 13b. Certificate (1-to-1 with EventRegistration)
        // ---------------------------------------------------------------------
        builder.Entity<Certificate>(entity =>
        {
            entity.Property(c => c.CertificateNumber).HasMaxLength(50).IsRequired();
            entity.Property(c => c.VerificationToken).HasMaxLength(100).IsRequired();
            entity.Property(c => c.PdfFilePath).HasMaxLength(500).IsRequired();
            entity.Property(c => c.RevocationReason).HasMaxLength(500);

            entity.HasIndex(c => c.RegistrationId).IsUnique();
            entity.HasIndex(c => c.CertificateNumber).IsUnique();
            entity.HasIndex(c => c.VerificationToken).IsUnique();
            entity.HasIndex(c => c.EventId);
            entity.HasIndex(c => c.UserId);
            entity.HasIndex(c => c.Status);

            entity.HasOne(c => c.Registration)
                  .WithOne(r => r.Certificate)
                  .HasForeignKey<Certificate>(c => c.RegistrationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Event)
                  .WithMany(e => e.Certificates)
                  .HasForeignKey(c => c.EventId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.User)
                  .WithMany(u => u.Certificates)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.IssuedByUser)
                  .WithMany()
                  .HasForeignKey(c => c.IssuedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.RevokedByUser)
                  .WithMany()
                  .HasForeignKey(c => c.RevokedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(c => !c.Event.IsDeleted);
        });

        // ---------------------------------------------------------------------
        // 14. Notification
        // ---------------------------------------------------------------------
        builder.Entity<Notification>(entity =>
        {
            entity.Property(n => n.Title).HasMaxLength(150).IsRequired();
            entity.Property(n => n.Message).HasMaxLength(1000).IsRequired();
            entity.Property(n => n.TargetRole).HasMaxLength(50);
            entity.Property(n => n.ActionUrl).HasMaxLength(500);
            entity.Property(n => n.CorrelationId).HasMaxLength(100);

            entity.HasIndex(n => n.UserId);
            entity.HasIndex(n => n.IsRead);
            entity.HasIndex(n => n.CorrelationId);

            entity.HasOne(n => n.User)
                  .WithMany(u => u.Notifications)
                  .HasForeignKey(n => n.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.Event)
                  .WithMany()
                  .HasForeignKey(n => n.EventId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------------------------------------------------------------------
        // 15. Announcement
        // ---------------------------------------------------------------------
        builder.Entity<Announcement>(entity =>
        {
            entity.Property(a => a.Title).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Content).HasMaxLength(4000).IsRequired();
            entity.Property(a => a.TargetRole).HasMaxLength(50);

            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => a.AudienceType);
            entity.HasIndex(a => a.DepartmentId);
            entity.HasIndex(a => a.EventId);
            entity.HasIndex(a => a.AuthorUserId);
            entity.HasIndex(a => a.ExpiresAt);

            entity.HasOne(a => a.AuthorUser)
                  .WithMany(u => u.AuthoredAnnouncements)
                  .HasForeignKey(a => a.AuthorUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Department)
                  .WithMany()
                  .HasForeignKey(a => a.DepartmentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Event)
                  .WithMany()
                  .HasForeignKey(a => a.EventId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------------------------------------------------------------------
        // 16. AuditLog
        // ---------------------------------------------------------------------
        builder.Entity<AuditLog>(entity =>
        {
            entity.Property(al => al.Action).HasMaxLength(100).IsRequired();
            entity.Property(al => al.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(al => al.EntityId).HasMaxLength(100);
            entity.Property(al => al.IpAddress).HasMaxLength(50);
            entity.Property(al => al.UserAgent).HasMaxLength(300);

            entity.HasIndex(al => al.Timestamp);
            entity.HasIndex(al => al.Action);

            entity.HasOne(al => al.User)
                  .WithMany(u => u.AuditLogs)
                  .HasForeignKey(al => al.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceSuperAdminProtection();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        EnforceSuperAdminProtection();
        return base.SaveChanges();
    }

    private void EnforceSuperAdminProtection()
    {
        // 1. Guard against deleting, deactivating, or locking out a protected Super Admin user
        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Deleted && entry.Entity.IsSuperAdminProtected)
            {
                throw new SuperAdminProtectionException("The primary Super Admin account is system-protected and cannot be deleted.");
            }

            if (entry.State == EntityState.Modified && entry.Entity.IsSuperAdminProtected)
            {
                if (entry.Property(u => u.IsActive).IsModified && !entry.Entity.IsActive)
                {
                    throw new SuperAdminProtectionException("The primary Super Admin account is system-protected and cannot be deactivated.");
                }

                if (entry.Property(u => u.LockoutEnd).IsModified &&
                    entry.Entity.LockoutEnd.HasValue &&
                    entry.Entity.LockoutEnd.Value > DateTimeOffset.UtcNow)
                {
                    throw new SuperAdminProtectionException("The primary Super Admin account is system-protected and cannot be locked out.");
                }
            }
        }

        // 2. Guard against removing the SuperAdmin role from a protected Super Admin
        foreach (var entry in ChangeTracker.Entries<IdentityUserRole<string>>())
        {
            if (entry.State == EntityState.Deleted)
            {
                var user = Users.AsNoTracking().FirstOrDefault(u => u.Id == entry.Entity.UserId);
                if (user != null && user.IsSuperAdminProtected)
                {
                    var superAdminRole = Roles.AsNoTracking().FirstOrDefault(r => r.Name == nameof(UserRole.SuperAdmin));
                    if (superAdminRole != null && entry.Entity.RoleId == superAdminRole.Id)
                    {
                        throw new SuperAdminProtectionException("The SuperAdmin role cannot be removed from the protected Super Admin account.");
                    }
                }
            }
        }
    }
}
