using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using CollegeEventRegistration.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
public class EventsController : Controller
{
    private readonly IEventManagementService _eventService;
    private readonly IDepartmentService _departmentService;
    private readonly IEventCategoryService _categoryService;
    private readonly IBannerUploadService _bannerUploadService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public EventsController(
        IEventManagementService eventService,
        IDepartmentService departmentService,
        IEventCategoryService categoryService,
        IBannerUploadService bannerUploadService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _eventService = eventService;
        _departmentService = departmentService;
        _categoryService = categoryService;
        _bannerUploadService = bannerUploadService;
        _context = context;
        _userManager = userManager;
    }

    // GET: /Admin/Events
    public async Task<IActionResult> Index(
        string? tab = "all",
        string? searchTerm = null,
        int? categoryId = null,
        int? departmentId = null,
        EventType? eventType = null,
        EventMode? eventMode = null,
        string? sortBy = "date_desc",
        int pageNumber = 1)
    {
        EventStatus? statusFilter = tab?.ToLowerInvariant() switch
        {
            "draft" => EventStatus.Draft,
            "pending" => EventStatus.PendingApproval,
            "approved" => EventStatus.Approved,
            "published" => EventStatus.Published,
            "rejected" => EventStatus.Rejected,
            "cancelled" => EventStatus.Cancelled,
            _ => null
        };

        var criteria = new EventFilterCriteria
        {
            SearchTerm = searchTerm,
            CategoryId = categoryId,
            DepartmentId = departmentId,
            Status = statusFilter,
            EventType = eventType,
            EventMode = eventMode,
            SortBy = sortBy,
            PageNumber = pageNumber,
            PageSize = 9
        };

        var pagedResult = await _eventService.GetEventsAsync(criteria);

        // Compute tab counts
        var baseQuery = _context.Events.Where(e => !e.IsDeleted);
        var tabCounts = await baseQuery
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        var categories = await _categoryService.GetAllCategoriesAsync();
        var departments = await _departmentService.GetAllDepartmentsAsync();

        var viewModel = new EventListViewModel
        {
            PagedEvents = pagedResult,
            SearchTerm = searchTerm,
            CategoryId = categoryId,
            DepartmentId = departmentId,
            Status = statusFilter,
            EventType = eventType,
            EventMode = eventMode,
            SortBy = sortBy,
            ActiveTab = tab?.ToLowerInvariant() ?? "all",
            IsElevatedAdmin = true,
            CountAll = await baseQuery.CountAsync(),
            CountDraft = tabCounts.TryGetValue(EventStatus.Draft, out var cd) ? cd : 0,
            CountPendingApproval = tabCounts.TryGetValue(EventStatus.PendingApproval, out var cp) ? cp : 0,
            CountApproved = tabCounts.TryGetValue(EventStatus.Approved, out var ca) ? ca : 0,
            CountPublished = tabCounts.TryGetValue(EventStatus.Published, out var cpub) ? cpub : 0,
            CountRejected = tabCounts.TryGetValue(EventStatus.Rejected, out var cr) ? cr : 0,
            CountCancelled = tabCounts.TryGetValue(EventStatus.Cancelled, out var cc) ? cc : 0,
            Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == categoryId)).ToList(),
            Departments = departments.Select(d => new SelectListItem($"{d.Name} ({d.Code})", d.Id.ToString(), d.Id == departmentId)).ToList()
        };

        return View(viewModel);
    }

    // GET: /Admin/Events/Approvals
    public async Task<IActionResult> Approvals(string? searchTerm, int pageNumber = 1)
    {
        return await Index(tab: "pending", searchTerm: searchTerm, pageNumber: pageNumber);
    }

    // GET: /Admin/Events/Create
    public async Task<IActionResult> Create()
    {
        var model = new EventWizardViewModel
        {
            RegistrationStartDate = DateTime.Today.AddDays(1).AddHours(9),
            RegistrationEndDate = DateTime.Today.AddDays(10).AddHours(18),
            EventStartDate = DateTime.Today.AddDays(14).AddHours(9),
            EventEndDate = DateTime.Today.AddDays(15).AddHours(17)
        };

        await PopulateLookupsAsync(model);
        return View(model);
    }

    // POST: /Admin/Events/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventWizardViewModel model, string? submitAction)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (submitAction == "SubmitForApproval")
        {
            model.SubmitForApproval = true;
        }

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(model);
            return View(model);
        }

        string? bannerUrl = null;
        if (model.BannerFile != null && model.BannerFile.Length > 0)
        {
            using var stream = model.BannerFile.OpenReadStream();
            var uploadResult = await _bannerUploadService.UploadBannerAsync(
                stream,
                model.BannerFile.FileName,
                model.BannerFile.ContentType,
                model.BannerFile.Length
            );

            if (!uploadResult.Succeeded)
            {
                ModelState.AddModelError(nameof(model.BannerFile), uploadResult.ErrorMessage ?? "Banner upload failed.");
                await PopulateLookupsAsync(model);
                return View(model);
            }

            bannerUrl = uploadResult.FilePath;
        }

        var dto = new CreateEventDto
        {
            Title = model.Title.Trim(),
            ShortDescription = model.ShortDescription.Trim(),
            Description = model.Description.Trim(),
            EventCategoryId = model.EventCategoryId,
            DepartmentId = model.DepartmentId,
            EventType = model.EventType,
            RegistrationFee = model.EventType == EventType.Free ? 0.00m : model.RegistrationFee,
            EventMode = model.EventMode,
            Venue = model.Venue?.Trim(),
            OnlineMeetingUrl = model.OnlineMeetingUrl?.Trim(),
            TotalCapacity = model.TotalCapacity,
            RegistrationStartDate = model.RegistrationStartDate,
            RegistrationEndDate = model.RegistrationEndDate,
            EventStartDate = model.EventStartDate,
            EventEndDate = model.EventEndDate,
            EligibleDepartmentIds = model.EligibleDepartmentIds ?? new List<int>(),
            YearOfStudy = model.YearOfStudy,
            MinimumCgpa = model.MinimumCgpa,
            IsOpenToOtherColleges = model.IsOpenToOtherColleges,
            EligibilityRemarks = model.EligibilityRemarks?.Trim(),
            Sessions = model.Sessions ?? new List<CreateSessionDto>(),
            CoordinatorUserIds = model.CoordinatorUserIds ?? new List<string>(),
            PrimaryCoordinatorUserId = model.PrimaryCoordinatorUserId,
            BannerImageUrl = bannerUrl,
            SubmitForApproval = model.SubmitForApproval
        };

        var result = await _eventService.CreateEventAsync(dto, currentUser.Id);

        if (!result.Succeeded)
        {
            if (!string.IsNullOrEmpty(bannerUrl))
            {
                await _bannerUploadService.DeleteBannerAsync(bannerUrl);
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create event.");
            await PopulateLookupsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = model.SubmitForApproval
            ? $"Event '{result.Event!.Title}' created and submitted for administrative approval."
            : $"Event '{result.Event!.Title}' saved as Draft successfully.";

        return RedirectToAction(nameof(Details), new { id = result.Event!.Id });
    }

    // GET: /Admin/Events/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var canEdit = await _eventService.CanUserEditEventAsync(id, currentUser.Id, isElevatedAdmin: true);
        if (!canEdit)
        {
            TempData["ErrorMessage"] = "You do not have permission to modify this event in its current status.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var ev = await _eventService.GetEventDetailsAsync(id);
        if (ev == null) return NotFound();

        var model = new EventWizardViewModel
        {
            Id = ev.Id,
            Title = ev.Title,
            ShortDescription = ev.ShortDescription,
            Description = ev.Description,
            EventCategoryId = ev.EventCategoryId,
            DepartmentId = ev.DepartmentId,
            EventType = ev.EventType,
            RegistrationFee = ev.RegistrationFee,
            EventMode = ev.EventMode,
            Venue = ev.Venue,
            OnlineMeetingUrl = ev.OnlineMeetingUrl,
            TotalCapacity = ev.TotalCapacity,
            RegistrationStartDate = ev.RegistrationStartDate,
            RegistrationEndDate = ev.RegistrationEndDate,
            EventStartDate = ev.EventStartDate,
            EventEndDate = ev.EventEndDate,
            EligibleDepartmentIds = ev.Eligibilities.Where(el => el.DepartmentId.HasValue).Select(el => el.DepartmentId!.Value).ToList(),
            YearOfStudy = ev.Eligibilities.FirstOrDefault()?.YearOfStudy,
            MinimumCgpa = ev.Eligibilities.FirstOrDefault()?.MinimumCgpa,
            IsOpenToOtherColleges = ev.Eligibilities.FirstOrDefault()?.IsOpenToOtherColleges ?? true,
            EligibilityRemarks = ev.Eligibilities.FirstOrDefault()?.Remarks,
            ExistingBannerUrl = ev.BannerImageUrl,
            CoordinatorUserIds = ev.Coordinators.Select(c => c.UserId).ToList(),
            PrimaryCoordinatorUserId = ev.Coordinators.FirstOrDefault(c => c.IsPrimaryCoordinator)?.UserId,
            Sessions = ev.Sessions.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime).Select(s => new CreateSessionDto
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

        await PopulateLookupsAsync(model);
        return View(model);
    }

    // POST: /Admin/Events/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventWizardViewModel model, string? submitAction)
    {
        if (id != model.Id) return BadRequest();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (submitAction == "SubmitForApproval")
        {
            model.SubmitForApproval = true;
        }

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(model);
            return View(model);
        }

        string? bannerUrl = model.ExistingBannerUrl;
        if (model.BannerFile != null && model.BannerFile.Length > 0)
        {
            using var stream = model.BannerFile.OpenReadStream();
            var uploadResult = await _bannerUploadService.UploadBannerAsync(
                stream,
                model.BannerFile.FileName,
                model.BannerFile.ContentType,
                model.BannerFile.Length
            );

            if (!uploadResult.Succeeded)
            {
                ModelState.AddModelError(nameof(model.BannerFile), uploadResult.ErrorMessage ?? "Banner upload failed.");
                await PopulateLookupsAsync(model);
                return View(model);
            }

            bannerUrl = uploadResult.FilePath;
        }

        var updateDto = new UpdateEventDto
        {
            Id = id,
            Title = model.Title.Trim(),
            ShortDescription = model.ShortDescription.Trim(),
            Description = model.Description.Trim(),
            EventCategoryId = model.EventCategoryId,
            DepartmentId = model.DepartmentId,
            EventType = model.EventType,
            RegistrationFee = model.EventType == EventType.Free ? 0.00m : model.RegistrationFee,
            EventMode = model.EventMode,
            Venue = model.Venue?.Trim(),
            OnlineMeetingUrl = model.OnlineMeetingUrl?.Trim(),
            TotalCapacity = model.TotalCapacity,
            RegistrationStartDate = model.RegistrationStartDate,
            RegistrationEndDate = model.RegistrationEndDate,
            EventStartDate = model.EventStartDate,
            EventEndDate = model.EventEndDate,
            EligibleDepartmentIds = model.EligibleDepartmentIds ?? new List<int>(),
            YearOfStudy = model.YearOfStudy,
            MinimumCgpa = model.MinimumCgpa,
            IsOpenToOtherColleges = model.IsOpenToOtherColleges,
            EligibilityRemarks = model.EligibilityRemarks?.Trim(),
            Sessions = model.Sessions ?? new List<CreateSessionDto>(),
            CoordinatorUserIds = model.CoordinatorUserIds ?? new List<string>(),
            PrimaryCoordinatorUserId = model.PrimaryCoordinatorUserId,
            BannerImageUrl = bannerUrl,
            SubmitForApproval = model.SubmitForApproval
        };

        var result = await _eventService.UpdateEventAsync(id, updateDto, currentUser.Id, isElevatedAdmin: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to update event.");
            await PopulateLookupsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = "Event updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: /Admin/Events/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var ev = await _eventService.GetEventDetailsAsync(id);
        if (ev == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        string currentUserId = currentUser?.Id ?? string.Empty;

        var auditTrail = await _context.AuditLogs
            .Include(a => a.User)
            .Where(a => a.EntityName == "Event" && a.EntityId == id.ToString())
            .OrderByDescending(a => a.Timestamp)
            .Take(15)
            .ToListAsync();

        var canApprove = await _eventService.CanUserApproveEventAsync(id, currentUserId);

        var viewModel = new EventDetailsViewModel
        {
            Event = ev,
            CanEdit = await _eventService.CanUserEditEventAsync(id, currentUserId, isElevatedAdmin: true),
            CanSubmitForApproval = ev.Status == EventStatus.Draft || ev.Status == EventStatus.Rejected,
            CanApprove = canApprove && ev.Status == EventStatus.PendingApproval,
            CanReject = canApprove && ev.Status == EventStatus.PendingApproval,
            CanPublish = ev.Status == EventStatus.Approved,
            CanCancel = ev.Status == EventStatus.Approved || ev.Status == EventStatus.Published,
            CanDelete = ev.Status == EventStatus.Draft || ev.Status == EventStatus.Cancelled,
            IsElevatedAdmin = true,
            IsAssignedCoordinator = ev.Coordinators.Any(c => c.UserId == currentUserId),
            AuditTrail = auditTrail
        };

        return View(viewModel);
    }

    // POST: /Admin/Events/Approve/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _eventService.ApproveEventAsync(id, currentUser.Id);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Event has been officially approved.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Admin/Events/Reject
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(EventApprovalActionViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (string.IsNullOrWhiteSpace(model.RejectionReason))
        {
            TempData["ErrorMessage"] = "A detailed rejection reason is mandatory when declining an event proposal.";
            return RedirectToAction(nameof(Details), new { id = model.EventId });
        }

        var result = await _eventService.RejectEventAsync(model.EventId, currentUser.Id, model.RejectionReason.Trim());
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Event proposal has been rejected and returned to the coordinator with feedback.";
        }

        return RedirectToAction(nameof(Details), new { id = model.EventId });
    }

    // POST: /Admin/Events/Publish/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _eventService.PublishEventAsync(id, currentUser.Id, isElevatedAdmin: true);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Event is now officially Published and open according to registration schedule.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Admin/Events/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var result = await _eventService.CancelEventAsync(id, currentUser.Id, reason ?? "Cancelled by administration.", isElevatedAdmin: true);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Event has been marked as Cancelled.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateLookupsAsync(EventWizardViewModel model)
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        var departments = await _departmentService.GetAllDepartmentsAsync();

        model.Categories = categories
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == model.EventCategoryId))
            .ToList();

        model.Departments = departments
            .Select(d => new SelectListItem($"{d.Name} ({d.Code})", d.Id.ToString(), d.Id == model.DepartmentId))
            .ToList();

        var coordinators = await _userManager.GetUsersInRoleAsync(nameof(UserRole.Coordinator));
        var admins = await _userManager.GetUsersInRoleAsync(nameof(UserRole.Admin));

        var combinedStaff = coordinators.Concat(admins)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();

        model.AvailableCoordinators = combinedStaff.Select(u => new CoordinatorLookupItem
        {
            Id = u.Id,
            FullName = $"{u.FirstName} {u.LastName}".Trim(),
            Email = u.Email ?? string.Empty,
            DepartmentName = !string.IsNullOrEmpty(u.Department) ? u.Department : "General"
        }).OrderBy(c => c.FullName).ToList();
    }
}
