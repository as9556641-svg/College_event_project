using System.Net;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AnnouncementService> _logger;

    public AnnouncementService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        IAuditLogService auditLogService,
        ILogger<AnnouncementService> logger)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<PagedResult<AnnouncementDto>> GetActiveAnnouncementsForStudentAsync(
        string studentUserId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var student = await _userManager.FindByIdAsync(studentUserId);
        if (student == null)
        {
            return new PagedResult<AnnouncementDto> { PageNumber = page, PageSize = pageSize };
        }

        // Student's registered event IDs
        var registeredEventIds = await _context.EventRegistrations
            .AsNoTracking()
            .Where(r => r.UserId == studentUserId && r.Status == RegistrationStatus.Confirmed && r.IsActive)
            .Select(r => r.EventId)
            .ToListAsync(cancellationToken);

        // Student's department ID
        int? studentDeptId = null;
        if (!string.IsNullOrEmpty(student.Department))
        {
            var dept = await _context.Departments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Name == student.Department || d.Code == student.Department, cancellationToken);
            if (dept != null)
            {
                studentDeptId = dept.Id;
            }
        }

        var now = DateTime.UtcNow;

        var query = _context.Announcements
            .AsNoTracking()
            .Include(a => a.Department)
            .Include(a => a.Event)
            .Include(a => a.AuthorUser)
            .Where(a => a.IsActive && a.Status == AnnouncementStatus.Published)
            .Where(a => a.ExpiresAt == null || a.ExpiresAt > now)
            .Where(a =>
                a.AudienceType == AnnouncementAudience.All ||
                (a.AudienceType == AnnouncementAudience.Role && a.TargetRole == "Student") ||
                (a.AudienceType == AnnouncementAudience.Department && a.DepartmentId != null && a.DepartmentId == studentDeptId) ||
                (a.AudienceType == AnnouncementAudience.EventParticipants && a.EventId != null && registeredEventIds.Contains(a.EventId.Value))
            );

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Priority)
            .ThenByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync(cancellationToken);

        return new PagedResult<AnnouncementDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<AnnouncementDto>> GetAnnouncementsForCoordinatorAsync(
        string coordinatorUserId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        // Events assigned to this coordinator
        var assignedEventIds = await _context.EventCoordinators
            .AsNoTracking()
            .Where(c => c.UserId == coordinatorUserId)
            .Select(c => c.EventId)
            .ToListAsync(cancellationToken);

        var query = _context.Announcements
            .AsNoTracking()
            .Include(a => a.Department)
            .Include(a => a.Event)
            .Include(a => a.AuthorUser)
            .Where(a => a.IsActive)
            .Where(a =>
                a.AuthorUserId == coordinatorUserId ||
                (a.EventId != null && assignedEventIds.Contains(a.EventId.Value)) ||
                a.AudienceType == AnnouncementAudience.All ||
                (a.AudienceType == AnnouncementAudience.Role && (a.TargetRole == "Coordinator" || a.TargetRole == "Staff"))
            );

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync(cancellationToken);

        return new PagedResult<AnnouncementDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<AnnouncementDto>> GetAllAnnouncementsAdminAsync(
        AnnouncementFilterDto filter,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Announcements
            .AsNoTracking()
            .Include(a => a.Department)
            .Include(a => a.Event)
            .Include(a => a.AuthorUser)
            .Where(a => a.IsActive);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(a => a.Title.Contains(term) || a.Content.Contains(term));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(a => a.Status == filter.Status.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(a => a.Priority == filter.Priority.Value);
        }

        if (filter.AudienceType.HasValue)
        {
            query = query.Where(a => a.AudienceType == filter.AudienceType.Value);
        }

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(a => a.DepartmentId == filter.DepartmentId.Value);
        }

        if (filter.EventId.HasValue)
        {
            query = query.Where(a => a.EventId == filter.EventId.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync(cancellationToken);

        return new PagedResult<AnnouncementDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<AnnouncementDto?> GetAnnouncementDetailsAsync(
        int id,
        string? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Announcements
            .AsNoTracking()
            .Include(a => a.Department)
            .Include(a => a.Event)
            .Include(a => a.AuthorUser)
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive, cancellationToken);

        if (entity == null) return null;

        return MapToDto(entity);
    }

    public async Task<AnnouncementResultDto> CreateAnnouncementAsync(
        CreateAnnouncementDto dto,
        string authorUserId,
        bool isStaff,
        CancellationToken cancellationToken = default)
    {
        var author = await _userManager.FindByIdAsync(authorUserId);
        if (author == null)
        {
            return AnnouncementResultDto.Fail("Author user was not found.");
        }

        var roles = await _userManager.GetRolesAsync(author);
        bool isAdmin = roles.Contains("Admin") || roles.Contains("SuperAdmin");
        bool isCoordinator = roles.Contains("Coordinator");

        // Coordinator Scoping Guard
        if (isCoordinator && !isAdmin)
        {
            if (dto.AudienceType != AnnouncementAudience.EventParticipants || !dto.EventId.HasValue)
            {
                _logger.LogWarning(
                    "Security Breach: Coordinator {UserId} attempted to create a campus-wide announcement", authorUserId);
                throw new UnauthorizedAccessException("Coordinators can only create announcements for their assigned events.");
            }

            bool isAssigned = await _context.EventCoordinators
                .AnyAsync(c => c.EventId == dto.EventId.Value && c.UserId == authorUserId, cancellationToken);

            if (!isAssigned)
            {
                _logger.LogWarning(
                    "Security Breach: Coordinator {UserId} attempted to create an announcement for unassigned Event #{EventId}",
                    authorUserId, dto.EventId.Value);
                throw new UnauthorizedAccessException("You are not assigned as a coordinator for this event.");
            }
        }

        // Stored XSS defense: HTML-encode and sanitize content before persistence
        var safeTitle = WebUtility.HtmlEncode(dto.Title.Trim());
        var safeContent = WebUtility.HtmlEncode(dto.Content.Trim());

        var announcement = new Announcement
        {
            Title = safeTitle,
            Content = safeContent,
            Priority = dto.Priority,
            AudienceType = dto.AudienceType,
            DepartmentId = dto.AudienceType == AnnouncementAudience.Department ? dto.DepartmentId : null,
            TargetRole = dto.AudienceType == AnnouncementAudience.Role ? dto.TargetRole : null,
            EventId = dto.AudienceType == AnnouncementAudience.EventParticipants ? dto.EventId : null,
            AuthorUserId = authorUserId,
            ExpiresAt = dto.ExpiresAt,
            Status = dto.PublishImmediately ? AnnouncementStatus.Published : AnnouncementStatus.Draft,
            PublishedAt = dto.PublishImmediately ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "CreateAnnouncement",
            "Announcement",
            announcement.Id.ToString(),
            authorUserId,
            newValues: $"Created announcement '{announcement.Title}' (Status: {announcement.Status}, Priority: {announcement.Priority})");

        // If published immediately, dispatch notifications to target audience
        if (announcement.Status == AnnouncementStatus.Published)
        {
            await DispatchAnnouncementNotificationsAsync(announcement, cancellationToken);
        }

        return AnnouncementResultDto.Ok(MapToDto(announcement));
    }

    public async Task<AnnouncementResultDto> UpdateAnnouncementAsync(
        int id,
        EditAnnouncementDto dto,
        string modifyingUserId,
        bool isStaff,
        CancellationToken cancellationToken = default)
    {
        var announcement = await _context.Announcements
            .Include(a => a.Event)
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive, cancellationToken);

        if (announcement == null)
        {
            return AnnouncementResultDto.Fail("Announcement not found.");
        }

        var modifyingUser = await _userManager.FindByIdAsync(modifyingUserId);
        if (modifyingUser == null)
        {
            return AnnouncementResultDto.Fail("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(modifyingUser);
        bool isAdmin = roles.Contains("Admin") || roles.Contains("SuperAdmin");
        bool isCoordinator = roles.Contains("Coordinator");

        // Authorization checks
        if (isCoordinator && !isAdmin)
        {
            if (announcement.AuthorUserId != modifyingUserId)
            {
                // Must coordinate the event
                if (!announcement.EventId.HasValue ||
                    !await _context.EventCoordinators.AnyAsync(c => c.EventId == announcement.EventId.Value && c.UserId == modifyingUserId, cancellationToken))
                {
                    throw new UnauthorizedAccessException("You are not authorized to modify this announcement.");
                }
            }

            if (dto.AudienceType != AnnouncementAudience.EventParticipants || !dto.EventId.HasValue)
            {
                throw new UnauthorizedAccessException("Coordinators can only manage announcements for assigned events.");
            }
        }

        // Stored XSS defense
        announcement.Title = WebUtility.HtmlEncode(dto.Title.Trim());
        announcement.Content = WebUtility.HtmlEncode(dto.Content.Trim());
        announcement.Priority = dto.Priority;
        announcement.AudienceType = dto.AudienceType;
        announcement.DepartmentId = dto.AudienceType == AnnouncementAudience.Department ? dto.DepartmentId : null;
        announcement.TargetRole = dto.AudienceType == AnnouncementAudience.Role ? dto.TargetRole : null;
        announcement.EventId = dto.AudienceType == AnnouncementAudience.EventParticipants ? dto.EventId : null;
        announcement.ExpiresAt = dto.ExpiresAt;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "UpdateAnnouncement",
            "Announcement",
            announcement.Id.ToString(),
            modifyingUserId,
            newValues: $"Updated announcement '{announcement.Title}'");

        return AnnouncementResultDto.Ok(MapToDto(announcement));
    }

    public async Task<bool> PublishAnnouncementAsync(
        int id,
        string modifyingUserId,
        bool isStaff,
        CancellationToken cancellationToken = default)
    {
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive, cancellationToken);

        if (announcement == null) return false;

        announcement.Status = AnnouncementStatus.Published;
        announcement.PublishedAt = DateTime.UtcNow;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "PublishAnnouncement",
            "Announcement",
            announcement.Id.ToString(),
            modifyingUserId,
            newValues: $"Published announcement '{announcement.Title}'");

        await DispatchAnnouncementNotificationsAsync(announcement, cancellationToken);

        return true;
    }

    public async Task<bool> ArchiveAnnouncementAsync(
        int id,
        string modifyingUserId,
        bool isStaff,
        CancellationToken cancellationToken = default)
    {
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive, cancellationToken);

        if (announcement == null) return false;

        announcement.Status = AnnouncementStatus.Archived;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "ArchiveAnnouncement",
            "Announcement",
            announcement.Id.ToString(),
            modifyingUserId,
            newValues: $"Archived announcement '{announcement.Title}'");

        return true;
    }

    public async Task<bool> DeleteAnnouncementAsync(
        int id,
        string modifyingUserId,
        bool isStaff,
        CancellationToken cancellationToken = default)
    {
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive, cancellationToken);

        if (announcement == null) return false;

        announcement.IsActive = false;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "DeleteAnnouncement",
            "Announcement",
            announcement.Id.ToString(),
            modifyingUserId,
            newValues: $"Deleted announcement '{announcement.Title}'");

        return true;
    }

    private async Task DispatchAnnouncementNotificationsAsync(
        Announcement announcement,
        CancellationToken cancellationToken)
    {
        try
        {
            var title = $"Announcement: {WebUtility.HtmlDecode(announcement.Title)}";
            var message = WebUtility.HtmlDecode(announcement.Content);
            if (message.Length > 200) message = message[..197] + "...";

            if (announcement.AudienceType == AnnouncementAudience.EventParticipants && announcement.EventId.HasValue)
            {
                await _notificationService.BroadcastEventNotificationAsync(
                    announcement.EventId.Value,
                    title,
                    message,
                    NotificationType.Announcement,
                    actionUrl: $"/Student/Announcements/Details/{announcement.Id}",
                    correlationPrefix: $"ANNC-{announcement.Id}",
                    cancellationToken: cancellationToken);
            }
            else if (announcement.AudienceType == AnnouncementAudience.Role && !string.IsNullOrEmpty(announcement.TargetRole))
            {
                await _notificationService.BroadcastRoleNotificationAsync(
                    announcement.TargetRole,
                    title,
                    message,
                    NotificationType.Announcement,
                    actionUrl: $"/Student/Announcements/Details/{announcement.Id}",
                    correlationPrefix: $"ANNC-{announcement.Id}",
                    cancellationToken: cancellationToken);
            }
            else if (announcement.AudienceType == AnnouncementAudience.All)
            {
                await _notificationService.BroadcastRoleNotificationAsync(
                    "Student",
                    title,
                    message,
                    NotificationType.Announcement,
                    actionUrl: $"/Student/Announcements/Details/{announcement.Id}",
                    correlationPrefix: $"ANNC-{announcement.Id}",
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast notifications for announcement #{AnnouncementId}", announcement.Id);
        }
    }

    private static AnnouncementDto MapToDto(Announcement a) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Content = a.Content,
        Priority = a.Priority,
        Status = a.Status,
        AudienceType = a.AudienceType,
        DepartmentId = a.DepartmentId,
        DepartmentName = a.Department?.Name,
        TargetRole = a.TargetRole,
        EventId = a.EventId,
        EventTitle = a.Event?.Title,
        AuthorUserId = a.AuthorUserId,
        AuthorName = a.AuthorUser?.FullName,
        PublishedAt = a.PublishedAt,
        ExpiresAt = a.ExpiresAt,
        CreatedAt = a.CreatedAt
    };
}
