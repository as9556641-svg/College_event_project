using System.ComponentModel.DataAnnotations;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CollegeEventRegistration.Web.Areas.Admin.Models;

public class EventListViewModel
{
    public PagedResult<Event> PagedEvents { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }
    public EventStatus? Status { get; set; }
    public EventType? EventType { get; set; }
    public EventMode? EventMode { get; set; }
    public string? SortBy { get; set; } = "date_desc";

    // Tab counts
    public int CountAll { get; set; }
    public int CountDraft { get; set; }
    public int CountPendingApproval { get; set; }
    public int CountApproved { get; set; }
    public int CountPublished { get; set; }
    public int CountRejected { get; set; }
    public int CountCancelled { get; set; }

    // Dropdown lookups
    public List<SelectListItem> Categories { get; set; } = new();
    public List<SelectListItem> Departments { get; set; } = new();

    // Context flags
    public bool IsElevatedAdmin { get; set; }
    public bool IsCoordinator { get; set; }
    public string? ActiveTab { get; set; }
}

public class EventWizardViewModel : IValidatableObject
{
    public int? Id { get; set; }
    public bool IsEditMode => Id.HasValue && Id.Value > 0;

    // STEP 1: Basic Information
    [Required(ErrorMessage = "Event Title is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
    [Display(Name = "Event Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Short summary is required.")]
    [StringLength(300, MinimumLength = 10, ErrorMessage = "Short summary must be between 10 and 300 characters.")]
    [Display(Name = "Short Summary")]
    public string ShortDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full description is required.")]
    [Display(Name = "Comprehensive Event Description")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select an Event Category.")]
    [Display(Name = "Category")]
    public int EventCategoryId { get; set; }

    [Required(ErrorMessage = "Please select an Organizing Department.")]
    [Display(Name = "Host Department")]
    public int DepartmentId { get; set; }

    // STEP 2: Event Details & Schedule
    [Required]
    [Display(Name = "Registration Type")]
    public EventType EventType { get; set; } = EventType.Free;

    [Range(0, 100000, ErrorMessage = "Registration Fee must be between 0 and 100,000.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Registration Fee (INR)")]
    public decimal RegistrationFee { get; set; } = 0.00m;

    [Required]
    [Display(Name = "Event Delivery Mode")]
    public EventMode EventMode { get; set; } = EventMode.Offline;

    [StringLength(200)]
    [Display(Name = "Physical Venue / Campus Room")]
    public string? Venue { get; set; }

    [Url(ErrorMessage = "Please enter a valid URL for virtual meetings.")]
    [StringLength(500)]
    [Display(Name = "Virtual Meeting URL (Zoom, Teams, Meet)")]
    public string? OnlineMeetingUrl { get; set; }

    [Required]
    [Range(1, 100000, ErrorMessage = "Total capacity must be at least 1.")]
    [Display(Name = "Participant Capacity")]
    public int TotalCapacity { get; set; } = 100;

    [Required(ErrorMessage = "Registration Start Date is required.")]
    [Display(Name = "Registration Open Date")]
    public DateTime RegistrationStartDate { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Registration End Date is required.")]
    [Display(Name = "Registration Close Date")]
    public DateTime RegistrationEndDate { get; set; } = DateTime.Now.AddDays(7);

    [Required(ErrorMessage = "Event Start Date is required.")]
    [Display(Name = "Event Start Date")]
    public DateTime EventStartDate { get; set; } = DateTime.Now.AddDays(8);

    [Required(ErrorMessage = "Event End Date is required.")]
    [Display(Name = "Event End Date")]
    public DateTime EventEndDate { get; set; } = DateTime.Now.AddDays(9);

    // STEP 3: Eligibility Rules
    [Display(Name = "Eligible Departments (Leave empty to open to all departments)")]
    public List<int> EligibleDepartmentIds { get; set; } = new();

    [Range(1, 5, ErrorMessage = "Year of study must be between 1 and 5.")]
    [Display(Name = "Target Year of Study (Optional)")]
    public int? YearOfStudy { get; set; }

    [Range(0.0, 10.0, ErrorMessage = "CGPA cutoff must be between 0.0 and 10.0.")]
    [Display(Name = "Minimum CGPA Requirement (Optional)")]
    public decimal? MinimumCgpa { get; set; }

    [Display(Name = "Open to external university/college students")]
    public bool IsOpenToOtherColleges { get; set; } = true;

    [StringLength(500)]
    [Display(Name = "Additional Eligibility Guidelines")]
    public string? EligibilityRemarks { get; set; }

    // STEP 4: Agenda & Sessions (JSON or dynamic list)
    public List<CreateSessionDto> Sessions { get; set; } = new();

    // STEP 5: Coordinators Assignment
    [Display(Name = "Assigned Coordinators")]
    public List<string> CoordinatorUserIds { get; set; } = new();

    [Display(Name = "Lead Coordinator")]
    public string? PrimaryCoordinatorUserId { get; set; }

    // STEP 6: Media Banner & Submission Action
    [Display(Name = "Event Banner Image (JPG, PNG, WEBP — Max 5MB)")]
    public IFormFile? BannerFile { get; set; }

    public string? ExistingBannerUrl { get; set; }

    [Display(Name = "Submit directly for Administrative Approval")]
    public bool SubmitForApproval { get; set; } = false;

    // Dropdown Lookups
    public List<SelectListItem> Categories { get; set; } = new();
    public List<SelectListItem> Departments { get; set; } = new();
    public List<CoordinatorLookupItem> AvailableCoordinators { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RegistrationStartDate >= RegistrationEndDate)
        {
            yield return new ValidationResult(
                "Registration start date must occur strictly before registration end date.",
                new[] { nameof(RegistrationStartDate), nameof(RegistrationEndDate) });
        }

        if (RegistrationEndDate > EventStartDate)
        {
            yield return new ValidationResult(
                "Registration must close on or before the event start date.",
                new[] { nameof(RegistrationEndDate), nameof(EventStartDate) });
        }

        if (EventStartDate > EventEndDate)
        {
            yield return new ValidationResult(
                "Event start date cannot be later than event end date.",
                new[] { nameof(EventStartDate), nameof(EventEndDate) });
        }

        if (EventType == EventType.Paid && RegistrationFee <= 0)
        {
            yield return new ValidationResult(
                "Registration fee must be greater than 0 for Paid events.",
                new[] { nameof(RegistrationFee) });
        }

        if ((EventMode == EventMode.Offline || EventMode == EventMode.Hybrid) && string.IsNullOrWhiteSpace(Venue))
        {
            yield return new ValidationResult(
                "Campus venue is required for In-Person and Hybrid events.",
                new[] { nameof(Venue) });
        }

        if ((EventMode == EventMode.Online || EventMode == EventMode.Hybrid) && string.IsNullOrWhiteSpace(OnlineMeetingUrl) && !IsEditMode)
        {
            // Optional warning or soft requirement
        }
    }
}

public class CoordinatorLookupItem
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
}

public class EventDetailsViewModel
{
    public Event Event { get; set; } = null!;
    public bool CanEdit { get; set; }
    public bool CanSubmitForApproval { get; set; }
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanPublish { get; set; }
    public bool CanCancel { get; set; }
    public bool CanDelete { get; set; }
    public bool IsElevatedAdmin { get; set; }
    public bool IsAssignedCoordinator { get; set; }
    public List<AuditLog> AuditTrail { get; set; } = new();
}

public class EventApprovalActionViewModel
{
    [Required]
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string Action { get; set; } = "Approve"; // Approve or Reject

    [StringLength(500, MinimumLength = 5, ErrorMessage = "Rejection reason must be between 5 and 500 characters.")]
    [Display(Name = "Rejection Reason (Required if rejecting)")]
    public string? RejectionReason { get; set; }
}

