using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.DTOs;

public class CreateEventDto
{
    // Step 1: Basic Information
    public string Title { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EventCategoryId { get; set; }
    public int DepartmentId { get; set; }

    // Step 2: Event Details & Schedule
    public EventType EventType { get; set; } = EventType.Free;
    public decimal RegistrationFee { get; set; } = 0.00m;
    public EventMode EventMode { get; set; } = EventMode.Offline;
    public string? Venue { get; set; }
    public string? OnlineMeetingUrl { get; set; }
    public int TotalCapacity { get; set; } = 100;
    public DateTime RegistrationStartDate { get; set; }
    public DateTime RegistrationEndDate { get; set; }
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }

    // Step 3: Eligibility Rules
    public List<int> EligibleDepartmentIds { get; set; } = new();
    public int? YearOfStudy { get; set; }
    public decimal? MinimumCgpa { get; set; }
    public bool IsOpenToOtherColleges { get; set; } = true;
    public string? EligibilityRemarks { get; set; }

    // Step 4: Sessions
    public List<CreateSessionDto> Sessions { get; set; } = new();

    // Step 5: Coordinators
    public List<string> CoordinatorUserIds { get; set; } = new();
    public string? PrimaryCoordinatorUserId { get; set; }

    // Step 6: Banner & Submission intent
    public string? BannerImageUrl { get; set; }
    public bool SubmitForApproval { get; set; } = false;
}

public class UpdateEventDto : CreateEventDto
{
    public int Id { get; set; }
}

public class CreateSessionDto
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
}

public class EventFilterCriteria
{
    public string? SearchTerm { get; set; }
    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }
    public EventStatus? Status { get; set; }
    public EventType? EventType { get; set; }
    public EventMode? EventMode { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CoordinatorUserId { get; set; }
    public bool OnlyPublishedAndOpen { get; set; } = false;
    public string? SortBy { get; set; } // date_asc, date_desc, title_asc, title_desc, capacity_desc
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Math.Max(1, PageSize));
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

