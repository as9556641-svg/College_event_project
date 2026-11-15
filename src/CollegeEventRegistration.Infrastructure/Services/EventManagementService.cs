using System.Text.RegularExpressions;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

public class EventManagementService : IEventManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<EventManagementService> _logger;
    private readonly INotificationService? _notificationService;

    public EventManagementService(
        ApplicationDbContext context,
        IAuditLogService auditLog,
        ILogger<EventManagementService> logger,
        INotificationService? notificationService = null)
    {
        _context = context;
        _auditLog = auditLog;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<(bool Succeeded, string? ErrorMessage, Event? Event)> CreateEventAsync(CreateEventDto dto, string createdByUserId)
    {
        // 1. Validation
        var validationError = ValidateEventSchedule(dto.RegistrationStartDate, dto.RegistrationEndDate, dto.EventStartDate, dto.EventEndDate);
        if (validationError != null)
            return (false, validationError, null);

        if (string.IsNullOrWhiteSpace(dto.Title))
            return (false, "Event title is required.", null);

        if (string.IsNullOrWhiteSpace(dto.ShortDescription))
            return (false, "Short description is required.", null);

        if (dto.TotalCapacity <= 0)
            return (false, "Total capacity must be greater than zero.", null);

        var deptExists = await _context.Departments.AnyAsync(d => d.Id == dto.DepartmentId && d.IsActive);
        if (!deptExists)
            return (false, "Selected organizing department is invalid or inactive.", null);

        var catExists = await _context.EventCategories.AnyAsync(c => c.Id == dto.EventCategoryId && c.IsActive);
        if (!catExists)
            return (false, "Selected event category is invalid or inactive.", null);

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync<(bool Succeeded, string? ErrorMessage, Event? Event)>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 2. Slug Generation
                    var baseSlug = GenerateSlug(dto.Title);
                    var slug = baseSlug;
                    int count = 1;
                    while (await _context.Events.IgnoreQueryFilters().AnyAsync(e => e.Slug == slug))
                    {
                        slug = $"{baseSlug}-{++count}";
                    }

                    // 3. Status determination
                    var initialStatus = dto.SubmitForApproval ? EventStatus.PendingApproval : EventStatus.Draft;

                    var evt = new Event
                    {
                        Title = dto.Title.Trim(),
                        Slug = slug,
                        ShortDescription = dto.ShortDescription.Trim(),
                        Description = dto.Description?.Trim() ?? string.Empty,
                        DepartmentId = dto.DepartmentId,
                        EventCategoryId = dto.EventCategoryId,
                        EventType = dto.EventType,
                        RegistrationFee = dto.EventType == EventType.Paid ? Math.Max(0, dto.RegistrationFee) : 0.00m,
                        EventMode = dto.EventMode,
                        Venue = dto.Venue?.Trim(),
                        OnlineMeetingUrl = dto.OnlineMeetingUrl?.Trim(),
                        BannerImageUrl = dto.BannerImageUrl?.Trim(),
                        TotalCapacity = dto.TotalCapacity,
                        RegisteredCount = 0,
                        RegistrationStartDate = dto.RegistrationStartDate,
                        RegistrationEndDate = dto.RegistrationEndDate,
                        EventStartDate = dto.EventStartDate,
                        EventEndDate = dto.EventEndDate,
                        Status = initialStatus,
                        IsApprovalRequired = true,
                        CreatedByUserId = createdByUserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Events.Add(evt);
                    await _context.SaveChangesAsync();

                    // 4. Add Eligibilities
                    if (dto.EligibleDepartmentIds != null && dto.EligibleDepartmentIds.Any())
                    {
                        foreach (var deptId in dto.EligibleDepartmentIds.Distinct())
                        {
                            _context.EventEligibilities.Add(new EventEligibility
                            {
                                EventId = evt.Id,
                                DepartmentId = deptId,
                                YearOfStudy = dto.YearOfStudy,
                                MinimumCgpa = dto.MinimumCgpa,
                                IsOpenToOtherColleges = dto.IsOpenToOtherColleges,
                                Remarks = dto.EligibilityRemarks?.Trim(),
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                    else
                    {
                        // Open to all departments with general conditions
                        _context.EventEligibilities.Add(new EventEligibility
                        {
                            EventId = evt.Id,
                            DepartmentId = null,
                            YearOfStudy = dto.YearOfStudy,
                            MinimumCgpa = dto.MinimumCgpa,
                            IsOpenToOtherColleges = dto.IsOpenToOtherColleges,
                            Remarks = dto.EligibilityRemarks?.Trim(),
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    // 5. Add Sessions
                    if (dto.Sessions != null && dto.Sessions.Any())
                    {
                        foreach (var s in dto.Sessions)
                        {
                            if (string.IsNullOrWhiteSpace(s.SessionTitle)) continue;

                            _context.EventSessions.Add(new EventSession
                            {
                                EventId = evt.Id,
                                SessionTitle = s.SessionTitle.Trim(),
                                Description = s.Description?.Trim(),
                                SpeakerName = s.SpeakerName?.Trim(),
                                SpeakerDesignation = s.SpeakerDesignation?.Trim(),
                                SpeakerOrganization = s.SpeakerOrganization?.Trim(),
                                SessionDate = s.SessionDate,
                                StartTime = s.StartTime,
                                EndTime = s.EndTime,
                                VenueOrRoom = s.VenueOrRoom?.Trim(),
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    // 6. Add Coordinators
                    if (dto.CoordinatorUserIds != null && dto.CoordinatorUserIds.Any())
                    {
                        var distinctCoordinators = dto.CoordinatorUserIds.Distinct().ToList();
                        foreach (var coordId in distinctCoordinators)
                        {
                            _context.EventCoordinators.Add(new EventCoordinator
                            {
                                EventId = evt.Id,
                                UserId = coordId,
                                IsPrimaryCoordinator = coordId == dto.PrimaryCoordinatorUserId,
                                AssignedAt = DateTime.UtcNow
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await _auditLog.LogAsync("CreateEvent", nameof(Event), evt.Id.ToString(), createdByUserId,
                        newValues: $"Title={evt.Title}, Status={evt.Status}, DepartmentId={evt.DepartmentId}");

                    _logger.LogInformation("Event successfully created: {Title} (ID {Id}) with status {Status}", evt.Title, evt.Id, evt.Status);
                    return (true, null, evt);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create event: {Title}", dto.Title);
            return (false, "An error occurred while saving the event. Please review the inputs and try again.", null);
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage, Event? Event)> UpdateEventAsync(int eventId, UpdateEventDto dto, string currentUserId, bool isElevatedAdmin)
    {
        var evt = await _context.Events
            .Include(e => e.Sessions)
            .Include(e => e.Eligibilities)
            .Include(e => e.Coordinators)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
            return (false, "Event not found.", null);

        var canEdit = await CanUserEditEventAsync(eventId, currentUserId, isElevatedAdmin);
        if (!canEdit)
            return (false, "You are not authorized to edit this event.", null);

        if (evt.Status == EventStatus.Completed || evt.Status == EventStatus.Cancelled)
            return (false, $"Cannot edit an event that is currently in '{evt.Status}' state.", null);

        var validationError = ValidateEventSchedule(dto.RegistrationStartDate, dto.RegistrationEndDate, dto.EventStartDate, dto.EventEndDate);
        if (validationError != null)
            return (false, validationError, null);

        if (dto.TotalCapacity < evt.RegisteredCount)
            return (false, $"Total capacity ({dto.TotalCapacity}) cannot be less than currently registered attendees ({evt.RegisteredCount}).", null);

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync<(bool Succeeded, string? ErrorMessage, Event? Event)>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var targetEvt = await _context.Events
                        .Include(e => e.Sessions)
                        .Include(e => e.Eligibilities)
                        .Include(e => e.Coordinators)
                        .FirstOrDefaultAsync(e => e.Id == eventId);

                    if (targetEvt == null)
                        return (false, "Event not found.", null);

                    var oldValues = $"Title={targetEvt.Title}, Status={targetEvt.Status}, Capacity={targetEvt.TotalCapacity}";

                    targetEvt.Title = dto.Title.Trim();
                    targetEvt.ShortDescription = dto.ShortDescription.Trim();
                    targetEvt.Description = dto.Description?.Trim() ?? string.Empty;
                    targetEvt.DepartmentId = dto.DepartmentId;
                    targetEvt.EventCategoryId = dto.EventCategoryId;
                    targetEvt.EventType = dto.EventType;
                    targetEvt.RegistrationFee = dto.EventType == EventType.Paid ? Math.Max(0, dto.RegistrationFee) : 0.00m;
                    targetEvt.EventMode = dto.EventMode;
                    targetEvt.Venue = dto.Venue?.Trim();
                    targetEvt.OnlineMeetingUrl = dto.OnlineMeetingUrl?.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.BannerImageUrl))
                    {
                        targetEvt.BannerImageUrl = dto.BannerImageUrl.Trim();
                    }
                    targetEvt.TotalCapacity = dto.TotalCapacity;
                    targetEvt.RegistrationStartDate = dto.RegistrationStartDate;
                    targetEvt.RegistrationEndDate = dto.RegistrationEndDate;
                    targetEvt.EventStartDate = dto.EventStartDate;
                    targetEvt.EventEndDate = dto.EventEndDate;
                    targetEvt.UpdatedAt = DateTime.UtcNow;

                    // If draft or rejected and user requested submission
                    if ((targetEvt.Status == EventStatus.Draft || targetEvt.Status == EventStatus.Rejected) && dto.SubmitForApproval)
                    {
                        targetEvt.Status = EventStatus.PendingApproval;
                        targetEvt.RejectionReason = null;
                    }

                    // Replace sessions
                    _context.EventSessions.RemoveRange(targetEvt.Sessions);
                    if (dto.Sessions != null && dto.Sessions.Any())
                    {
                        foreach (var s in dto.Sessions)
                        {
                            if (string.IsNullOrWhiteSpace(s.SessionTitle)) continue;
                            _context.EventSessions.Add(new EventSession
                            {
                                EventId = targetEvt.Id,
                                SessionTitle = s.SessionTitle.Trim(),
                                Description = s.Description?.Trim(),
                                SpeakerName = s.SpeakerName?.Trim(),
                                SpeakerDesignation = s.SpeakerDesignation?.Trim(),
                                SpeakerOrganization = s.SpeakerOrganization?.Trim(),
                                SessionDate = s.SessionDate,
                                StartTime = s.StartTime,
                                EndTime = s.EndTime,
                                VenueOrRoom = s.VenueOrRoom?.Trim(),
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    // Replace eligibilities
                    _context.EventEligibilities.RemoveRange(targetEvt.Eligibilities);
                    if (dto.EligibleDepartmentIds != null && dto.EligibleDepartmentIds.Any())
                    {
                        foreach (var deptId in dto.EligibleDepartmentIds.Distinct())
                        {
                            _context.EventEligibilities.Add(new EventEligibility
                            {
                                EventId = targetEvt.Id,
                                DepartmentId = deptId,
                                YearOfStudy = dto.YearOfStudy,
                                MinimumCgpa = dto.MinimumCgpa,
                                IsOpenToOtherColleges = dto.IsOpenToOtherColleges,
                                Remarks = dto.EligibilityRemarks?.Trim(),
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                    else
                    {
                        _context.EventEligibilities.Add(new EventEligibility
                        {
                            EventId = targetEvt.Id,
                            DepartmentId = null,
                            YearOfStudy = dto.YearOfStudy,
                            MinimumCgpa = dto.MinimumCgpa,
                            IsOpenToOtherColleges = dto.IsOpenToOtherColleges,
                            Remarks = dto.EligibilityRemarks?.Trim(),
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    // Replace coordinators if user is elevated admin
                    if (isElevatedAdmin && dto.CoordinatorUserIds != null)
                    {
                        _context.EventCoordinators.RemoveRange(targetEvt.Coordinators);
                        var distinctCoords = dto.CoordinatorUserIds.Distinct().ToList();
                        foreach (var cid in distinctCoords)
                        {
                            _context.EventCoordinators.Add(new EventCoordinator
                            {
                                EventId = targetEvt.Id,
                                UserId = cid,
                                IsPrimaryCoordinator = cid == dto.PrimaryCoordinatorUserId,
                                AssignedAt = DateTime.UtcNow
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await _auditLog.LogAsync("UpdateEvent", nameof(Event), targetEvt.Id.ToString(), currentUserId,
                        oldValues: oldValues,
                        newValues: $"Title={targetEvt.Title}, Status={targetEvt.Status}, Capacity={targetEvt.TotalCapacity}");

                    return (true, null, targetEvt);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update event: {Id}", eventId);
            return (false, "An error occurred while updating the event.", null);
        }
    }

    public async Task<Event?> GetEventDetailsAsync(int eventId, bool includeDeleted = false)
    {
        IQueryable<Event> query = _context.Events;
        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .Include(e => e.Department)
            .Include(e => e.EventCategory)
            .Include(e => e.CreatedByUser)
            .Include(e => e.ApprovedByUser)
            .Include(e => e.Sessions.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime))
            .Include(e => e.Eligibilities)
                .ThenInclude(ee => ee.Department)
            .Include(e => e.Coordinators)
                .ThenInclude(ec => ec.User)
            .FirstOrDefaultAsync(e => e.Id == eventId);
    }

    public async Task<PagedResult<Event>> GetEventsAsync(EventFilterCriteria criteria)
    {
        IQueryable<Event> query = _context.Events
            .Include(e => e.Department)
            .Include(e => e.EventCategory)
            .Include(e => e.CreatedByUser)
            .Include(e => e.Coordinators)
            .AsNoTracking();

        if (criteria.OnlyPublishedAndOpen)
        {
            query = query.Where(e => e.Status == EventStatus.Published && e.EventEndDate >= DateTime.UtcNow);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim();
            query = query.Where(e => e.Title.Contains(term) || e.ShortDescription.Contains(term) || (e.Venue != null && e.Venue.Contains(term)));
        }

        if (criteria.CategoryId.HasValue)
        {
            query = query.Where(e => e.EventCategoryId == criteria.CategoryId.Value);
        }

        if (criteria.DepartmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == criteria.DepartmentId.Value);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(e => e.Status == criteria.Status.Value);
        }

        if (criteria.EventType.HasValue)
        {
            query = query.Where(e => e.EventType == criteria.EventType.Value);
        }

        if (criteria.EventMode.HasValue)
        {
            query = query.Where(e => e.EventMode == criteria.EventMode.Value);
        }

        if (criteria.FromDate.HasValue)
        {
            query = query.Where(e => e.EventStartDate >= criteria.FromDate.Value);
        }

        if (criteria.ToDate.HasValue)
        {
            query = query.Where(e => e.EventStartDate <= criteria.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.CreatedByUserId))
        {
            query = query.Where(e => e.CreatedByUserId == criteria.CreatedByUserId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.CoordinatorUserId))
        {
            query = query.Where(e => e.Coordinators.Any(c => c.UserId == criteria.CoordinatorUserId));
        }

        // Sorting
        query = criteria.SortBy switch
        {
            "date_asc" => query.OrderBy(e => e.EventStartDate),
            "date_desc" => query.OrderByDescending(e => e.EventStartDate),
            "title_asc" => query.OrderBy(e => e.Title),
            "title_desc" => query.OrderByDescending(e => e.Title),
            "capacity_desc" => query.OrderByDescending(e => e.TotalCapacity),
            _ => query.OrderByDescending(e => e.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var pageSize = Math.Max(1, criteria.PageSize);
        var pageNumber = Math.Max(1, criteria.PageNumber);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Event>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> SubmitForApprovalAsync(int eventId, string currentUserId, bool isElevatedAdmin)
    {
        var evt = await _context.Events.FindAsync(eventId);
        if (evt == null)
            return (false, "Event not found.");

        var canEdit = await CanUserEditEventAsync(eventId, currentUserId, isElevatedAdmin);
        if (!canEdit)
            return (false, "You are not authorized to submit this event for approval.");

        if (evt.Status != EventStatus.Draft && evt.Status != EventStatus.Rejected)
            return (false, $"Cannot submit event in '{evt.Status}' state. Only Draft or Rejected events may be submitted.");

        evt.Status = EventStatus.PendingApproval;
        evt.RejectionReason = null;
        evt.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("SubmitForApproval", nameof(Event), evt.Id.ToString(), currentUserId,
            newValues: "Status=PendingApproval");

        _logger.LogInformation("Event ID {Id} submitted for approval by {User}", eventId, currentUserId);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> ApproveEventAsync(int eventId, string adminUserId)
    {
        var evt = await _context.Events
            .Include(e => e.Coordinators)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
            return (false, "Event not found.");

        if (evt.Status != EventStatus.PendingApproval)
            return (false, $"Event cannot be approved because it is currently '{evt.Status}', not 'PendingApproval'.");

        var canApprove = await CanUserApproveEventAsync(eventId, adminUserId);
        if (!canApprove)
            return (false, "Conflict of interest: You cannot approve an event you created or are assigned to coordinate. Another administrator must review this event.");

        evt.Status = EventStatus.Approved;
        evt.ApprovedByUserId = adminUserId;
        evt.ApprovedAt = DateTime.UtcNow;
        evt.RejectionReason = null;
        evt.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("ApproveEvent", nameof(Event), evt.Id.ToString(), adminUserId,
            newValues: $"Status=Approved, ApprovedBy={adminUserId}");

        _logger.LogInformation("Event ID {Id} approved by Admin {User}", eventId, adminUserId);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> RejectEventAsync(int eventId, string adminUserId, string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
            return (false, "A reason for rejection must be provided.");

        var evt = await _context.Events
            .Include(e => e.Coordinators)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
            return (false, "Event not found.");

        if (evt.Status != EventStatus.PendingApproval)
            return (false, $"Event cannot be rejected because it is currently '{evt.Status}', not 'PendingApproval'.");

        var canApprove = await CanUserApproveEventAsync(eventId, adminUserId);
        if (!canApprove)
            return (false, "Conflict of interest: You cannot review an event you created or are assigned to coordinate.");

        evt.Status = EventStatus.Rejected;
        evt.RejectionReason = rejectionReason.Trim();
        evt.ApprovedByUserId = adminUserId;
        evt.ApprovedAt = DateTime.UtcNow;
        evt.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("RejectEvent", nameof(Event), evt.Id.ToString(), adminUserId,
            newValues: $"Status=Rejected, Reason={evt.RejectionReason}");

        _logger.LogInformation("Event ID {Id} rejected by Admin {User}. Reason: {Reason}", eventId, adminUserId, rejectionReason);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> PublishEventAsync(int eventId, string currentUserId, bool isElevatedAdmin)
    {
        var evt = await _context.Events.FindAsync(eventId);
        if (evt == null)
            return (false, "Event not found.");

        if (evt.Status != EventStatus.Approved && !(isElevatedAdmin && evt.Status == EventStatus.Draft))
            return (false, $"Event cannot be published. Current status is '{evt.Status}'. It must be 'Approved' before publication.");

        evt.Status = EventStatus.Published;
        evt.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("PublishEvent", nameof(Event), evt.Id.ToString(), currentUserId,
            newValues: "Status=Published");

        _logger.LogInformation("Event ID {Id} published by {User}", eventId, currentUserId);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> CancelEventAsync(int eventId, string currentUserId, string reason, bool isElevatedAdmin)
    {
        var evt = await _context.Events.FindAsync(eventId);
        if (evt == null)
            return (false, "Event not found.");

        var canEdit = await CanUserEditEventAsync(eventId, currentUserId, isElevatedAdmin);
        if (!canEdit)
            return (false, "You are not authorized to cancel this event.");

        if (evt.Status == EventStatus.Completed)
            return (false, "Cannot cancel an event that has already completed.");

        evt.Status = EventStatus.Cancelled;
        evt.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by organizer" : reason.Trim();
        evt.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("CancelEvent", nameof(Event), evt.Id.ToString(), currentUserId,
            newValues: $"Status=Cancelled, Reason={evt.RejectionReason}");

        _logger.LogInformation("Event ID {Id} cancelled by {User}", eventId, currentUserId);

        if (_notificationService != null)
        {
            try
            {
                await _notificationService.BroadcastEventNotificationAsync(
                    eventId,
                    $"Event Cancelled: {evt.Title}",
                    $"The event '{evt.Title}' has been cancelled. Reason: {evt.RejectionReason}",
                    NotificationType.EventUpdate,
                    actionUrl: $"/Student/Events/Details/{evt.Id}",
                    correlationPrefix: $"EVT-CANCEL-{evt.Id}");
            }
            catch (Exception nEx)
            {
                _logger.LogWarning(nEx, "Failed to broadcast event cancellation notification for Event #{EventId}", eventId);
            }
        }

        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> AssignCoordinatorsAsync(
        int eventId,
        List<string> coordinatorUserIds,
        string? primaryCoordinatorUserId,
        string currentUserId,
        bool isElevatedAdmin)
    {
        if (!isElevatedAdmin)
            return (false, "Only administrative users can assign event coordinators.");

        var evt = await _context.Events
            .Include(e => e.Coordinators)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
            return (false, "Event not found.");

        _context.EventCoordinators.RemoveRange(evt.Coordinators);

        if (coordinatorUserIds != null && coordinatorUserIds.Any())
        {
            var distinctCoords = coordinatorUserIds.Distinct().ToList();
            foreach (var uid in distinctCoords)
            {
                _context.EventCoordinators.Add(new EventCoordinator
                {
                    EventId = evt.Id,
                    UserId = uid,
                    IsPrimaryCoordinator = uid == primaryCoordinatorUserId,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        await _auditLog.LogAsync("AssignCoordinators", nameof(Event), evt.Id.ToString(), currentUserId,
            newValues: $"CoordinatorsCount={coordinatorUserIds?.Count ?? 0}, Primary={primaryCoordinatorUserId}");

        return (true, null);
    }

    public async Task<bool> CanUserEditEventAsync(int eventId, string userId, bool isElevatedAdmin)
    {
        if (isElevatedAdmin) return true;

        var evt = await _context.Events
            .Include(e => e.Coordinators)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null) return false;

        // Non-elevated users (creators/coordinators) can only edit events in Draft or Rejected status
        if (evt.Status != EventStatus.Draft && evt.Status != EventStatus.Rejected)
        {
            return false;
        }

        if (evt.CreatedByUserId == userId) return true;

        if (evt.Coordinators.Any(c => c.UserId == userId)) return true;

        return false;
    }

    public async Task<bool> CanUserApproveEventAsync(int eventId, string userId)
    {
        var evt = await _context.Events
            .Include(e => e.Coordinators)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null) return false;

        // Creator cannot approve own event
        if (evt.CreatedByUserId == userId) return false;

        // Assigned coordinator cannot approve event
        if (evt.Coordinators.Any(c => c.UserId == userId)) return false;

        return true;
    }

    private static string? ValidateEventSchedule(
        DateTime regStart,
        DateTime regEnd,
        DateTime eventStart,
        DateTime eventEnd)
    {
        if (regStart >= regEnd)
            return "Registration start date must be earlier than registration end date.";

        if (regEnd > eventStart)
            return "Registration deadline cannot be after the event start date.";

        if (eventStart > eventEnd)
            return "Event start date must be earlier than or equal to event end date.";

        return null;
    }

    private static string GenerateSlug(string text)
    {
        var str = text.ToLowerInvariant();
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        str = Regex.Replace(str, @"\s+", " ").Trim();
        str = Regex.Replace(str, @"\s", "-");
        return string.IsNullOrEmpty(str) ? "event" : str;
    }
}

