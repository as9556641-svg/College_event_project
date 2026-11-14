using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Web.Areas.Student.Models;

/// <summary>
/// ViewModel for the Student Event Discovery catalog and search page.
/// </summary>
public class StudentEventCatalogViewModel
{
    public StudentEventQueryCriteria Criteria { get; set; } = new();
    public PagedResult<StudentEventCardViewModel> Events { get; set; } = new();
    public IReadOnlyList<CategoryWithCountDto> Categories { get; set; } = new List<CategoryWithCountDto>();
    public IReadOnlyList<Department> Departments { get; set; } = new List<Department>();
    public ApplicationUser? CurrentUser { get; set; }

    public int TotalEventsCount => Events.TotalCount;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Criteria.SearchTerm) ||
        (Criteria.CategoryId.HasValue && Criteria.CategoryId.Value > 0) ||
        (Criteria.DepartmentId.HasValue && Criteria.DepartmentId.Value > 0) ||
        Criteria.EventMode.HasValue ||
        Criteria.EventType.HasValue ||
        Criteria.OnlyRegistrationOpen ||
        (!string.IsNullOrWhiteSpace(Criteria.DateFilter) && Criteria.DateFilter != "all") ||
        Criteria.FromDate.HasValue ||
        Criteria.ToDate.HasValue;
}

/// <summary>
/// Individual event card ViewModel for the discovery grid.
/// </summary>
public class StudentEventCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string? BannerImageUrl { get; set; }

    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryIconClass { get; set; }

    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;

    public EventType EventType { get; set; }
    public decimal RegistrationFee { get; set; }
    public EventMode EventMode { get; set; }
    public string? Venue { get; set; }

    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public DateTime RegistrationStartDate { get; set; }
    public DateTime RegistrationEndDate { get; set; }

    public int TotalCapacity { get; set; }
    public int RegisteredCount { get; set; }
    public int RemainingSeats => Math.Max(0, TotalCapacity - RegisteredCount);

    public bool IsRegistrationOpen => DateTime.UtcNow >= RegistrationStartDate && DateTime.UtcNow <= RegistrationEndDate;
    public bool IsRegistrationUpcoming => DateTime.UtcNow < RegistrationStartDate;
    public bool IsRegistrationClosed => DateTime.UtcNow > RegistrationEndDate;
    public bool IsSoldOut => RemainingSeats <= 0;

    public string RegistrationBadgeText
    {
        get
        {
            if (IsSoldOut) return "Sold Out";
            if (IsRegistrationOpen) return "Registration Open";
            if (IsRegistrationUpcoming) return "Registration Soon";
            return "Registration Closed";
        }
    }

    public string RegistrationBadgeCssClass
    {
        get
        {
            if (IsSoldOut) return "bg-danger text-white";
            if (IsRegistrationOpen) return "bg-success text-white";
            if (IsRegistrationUpcoming) return "bg-info text-dark";
            return "bg-secondary text-white";
        }
    }

    public string FormattedEventDate => EventStartDate.FormatKolkata("ddd, dd MMM yyyy");
    public string FormattedEventTime => EventStartDate.FormatKolkata("hh:mm tt");
    public string FormattedRegistrationDeadline => RegistrationEndDate.FormatKolkata("dd MMM yyyy, hh:mm tt");

    public static StudentEventCardViewModel FromEntity(Event evt)
    {
        return new StudentEventCardViewModel
        {
            Id = evt.Id,
            Title = evt.Title,
            Slug = evt.Slug,
            ShortDescription = evt.ShortDescription,
            BannerImageUrl = evt.BannerImageUrl,
            CategoryId = evt.EventCategoryId,
            CategoryName = evt.EventCategory?.Name ?? "General",
            CategoryIconClass = evt.EventCategory?.IconClass ?? "bi-calendar-event",
            DepartmentId = evt.DepartmentId,
            DepartmentName = evt.Department?.Name ?? "General",
            EventType = evt.EventType,
            RegistrationFee = evt.RegistrationFee,
            EventMode = evt.EventMode,
            Venue = evt.Venue,
            EventStartDate = evt.EventStartDate,
            EventEndDate = evt.EventEndDate,
            RegistrationStartDate = evt.RegistrationStartDate,
            RegistrationEndDate = evt.RegistrationEndDate,
            TotalCapacity = evt.TotalCapacity,
            RegisteredCount = evt.RegisteredCount
        };
    }
}

/// <summary>
/// Comprehensive event details ViewModel for students.
/// </summary>
public class StudentEventDetailsViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? BannerImageUrl { get; set; }

    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryIconClass { get; set; }

    public string DepartmentName { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;

    public EventType EventType { get; set; }
    public decimal RegistrationFee { get; set; }
    public EventMode EventMode { get; set; }
    public string? Venue { get; set; }
    public string? OnlineMeetingUrl { get; set; }

    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public DateTime RegistrationStartDate { get; set; }
    public DateTime RegistrationEndDate { get; set; }

    public int TotalCapacity { get; set; }
    public int RegisteredCount { get; set; }
    public int RemainingSeats => Math.Max(0, TotalCapacity - RegisteredCount);

    public bool IsRegistrationOpen => DateTime.UtcNow >= RegistrationStartDate && DateTime.UtcNow <= RegistrationEndDate;
    public bool IsRegistrationUpcoming => DateTime.UtcNow < RegistrationStartDate;
    public bool IsRegistrationClosed => DateTime.UtcNow > RegistrationEndDate;
    public bool IsSoldOut => RemainingSeats <= 0;

    public IReadOnlyList<EventSessionItemViewModel> Sessions { get; set; } = new List<EventSessionItemViewModel>();
    public StudentEventEligibilityResult EligibilityResult { get; set; } = new();
    public ApplicationUser? CurrentUser { get; set; }

    public string FormattedEventStart => EventStartDate.FormatKolkata("dddd, dd MMM yyyy, hh:mm tt");
    public string FormattedEventEnd => EventEndDate.FormatKolkata("dddd, dd MMM yyyy, hh:mm tt");
    public string FormattedRegistrationStart => RegistrationStartDate.FormatKolkata("dd MMM yyyy, hh:mm tt");
    public string FormattedRegistrationEnd => RegistrationEndDate.FormatKolkata("dd MMM yyyy, hh:mm tt");

    public static StudentEventDetailsViewModel FromEntity(Event evt, StudentEventEligibilityResult eligibility, ApplicationUser? user)
    {
        return new StudentEventDetailsViewModel
        {
            Id = evt.Id,
            Title = evt.Title,
            Slug = evt.Slug,
            ShortDescription = evt.ShortDescription,
            Description = evt.Description,
            BannerImageUrl = evt.BannerImageUrl,
            CategoryName = evt.EventCategory?.Name ?? "General",
            CategoryIconClass = evt.EventCategory?.IconClass ?? "bi-calendar-event",
            DepartmentName = evt.Department?.Name ?? "General",
            DepartmentCode = evt.Department?.Code ?? "GEN",
            EventType = evt.EventType,
            RegistrationFee = evt.RegistrationFee,
            EventMode = evt.EventMode,
            Venue = evt.Venue,
            OnlineMeetingUrl = evt.OnlineMeetingUrl,
            EventStartDate = evt.EventStartDate,
            EventEndDate = evt.EventEndDate,
            RegistrationStartDate = evt.RegistrationStartDate,
            RegistrationEndDate = evt.RegistrationEndDate,
            TotalCapacity = evt.TotalCapacity,
            RegisteredCount = evt.RegisteredCount,
            EligibilityResult = eligibility,
            CurrentUser = user,
            Sessions = evt.Sessions
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.StartTime)
                .Select(s => new EventSessionItemViewModel
                {
                    SessionTitle = s.SessionTitle,
                    Description = s.Description,
                    SpeakerName = s.SpeakerName,
                    SpeakerDesignation = s.SpeakerDesignation,
                    SpeakerOrganization = s.SpeakerOrganization,
                    SessionDate = s.SessionDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    VenueOrRoom = s.VenueOrRoom
                }).ToList()
        };
    }
}

/// <summary>
/// Individual session track within an event details view.
/// </summary>
public class EventSessionItemViewModel
{
    public string SessionTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SpeakerName { get; set; }
    public string? SpeakerDesignation { get; set; }
    public string? SpeakerOrganization { get; set; }
    public DateTime SessionDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? VenueOrRoom { get; set; }

    public string FormattedDate => SessionDate.ToString("dd MMM yyyy");
    public string FormattedTimeRange =>
        $"{DateTime.Today.Add(StartTime):hh:mm tt} - {DateTime.Today.Add(EndTime):hh:mm tt} IST";
}
