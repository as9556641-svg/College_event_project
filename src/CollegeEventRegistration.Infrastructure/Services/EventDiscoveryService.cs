using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CollegeEventRegistration.Infrastructure.Services;

/// <summary>
/// Domain service implementing student-facing event discovery, searching, multi-criteria filtering,
/// and student eligibility evaluation with strict Published-only visibility enforcement.
/// </summary>
public class EventDiscoveryService : IEventDiscoveryService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<EventDiscoveryService> _logger;

    public EventDiscoveryService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<EventDiscoveryService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<Event>> GetPublishedEventsAsync(StudentEventQueryCriteria criteria)
    {
        var pageNumber = Math.Max(1, criteria.PageNumber);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 50);

        // Strict Server-Side Visibility Enforcement: Only Published & Non-Deleted Events
        IQueryable<Event> query = _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.Status == EventStatus.Published);

        // Filter: Registration Currently Open
        var utcNow = DateTime.UtcNow;
        if (criteria.OnlyRegistrationOpen)
        {
            query = query.Where(e => e.RegistrationStartDate <= utcNow && e.RegistrationEndDate >= utcNow);
        }

        // Filter: Search Term across Title, ShortDescription, Description, Venue
        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim();
            query = query.Where(e => e.Title.Contains(term)
                || e.ShortDescription.Contains(term)
                || e.Description.Contains(term)
                || (e.Venue != null && e.Venue.Contains(term)));
        }

        // Filter: Category
        if (criteria.CategoryId.HasValue && criteria.CategoryId.Value > 0)
        {
            query = query.Where(e => e.EventCategoryId == criteria.CategoryId.Value);
        }

        // Filter: Department
        if (criteria.DepartmentId.HasValue && criteria.DepartmentId.Value > 0)
        {
            query = query.Where(e => e.DepartmentId == criteria.DepartmentId.Value);
        }

        // Filter: Event Mode (Offline, Online, Hybrid)
        if (criteria.EventMode.HasValue)
        {
            query = query.Where(e => e.EventMode == criteria.EventMode.Value);
        }

        // Filter: Event Type (Free, Paid)
        if (criteria.EventType.HasValue)
        {
            query = query.Where(e => e.EventType == criteria.EventType.Value);
        }

        // Filter: Date Range & Quick Filters
        if (!string.IsNullOrWhiteSpace(criteria.DateFilter) && criteria.DateFilter != "all")
        {
            switch (criteria.DateFilter.ToLowerInvariant())
            {
                case "today":
                    var startOfToday = utcNow.Date;
                    var endOfToday = startOfToday.AddDays(1);
                    query = query.Where(e => e.EventStartDate >= startOfToday && e.EventStartDate < endOfToday);
                    break;

                case "this_week":
                    var startOfWeek = utcNow.Date.AddDays(-(int)utcNow.DayOfWeek);
                    var endOfWeek = startOfWeek.AddDays(7);
                    query = query.Where(e => e.EventStartDate >= startOfWeek && e.EventStartDate < endOfWeek);
                    break;

                case "this_month":
                    var startOfMonth = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var endOfMonth = startOfMonth.AddMonths(1);
                    query = query.Where(e => e.EventStartDate >= startOfMonth && e.EventStartDate < endOfMonth);
                    break;

                case "custom":
                    if (criteria.FromDate.HasValue)
                        query = query.Where(e => e.EventStartDate >= criteria.FromDate.Value);
                    if (criteria.ToDate.HasValue)
                        query = query.Where(e => e.EventStartDate <= criteria.ToDate.Value);
                    break;
            }
        }
        else
        {
            if (criteria.FromDate.HasValue)
                query = query.Where(e => e.EventStartDate >= criteria.FromDate.Value);
            if (criteria.ToDate.HasValue)
                query = query.Where(e => e.EventStartDate <= criteria.ToDate.Value);
        }

        // Sorting
        query = (criteria.SortBy?.ToLowerInvariant()) switch
        {
            "latest" => query.OrderByDescending(e => e.ApprovedAt ?? e.CreatedAt),
            "date_asc" => query.OrderBy(e => e.EventStartDate),
            "date_desc" => query.OrderByDescending(e => e.EventStartDate),
            "title_asc" => query.OrderBy(e => e.Title),
            "title_desc" => query.OrderByDescending(e => e.Title),
            "upcoming" => query.OrderBy(e => e.EventStartDate >= utcNow ? 0 : 1)
                               .ThenBy(e => e.EventStartDate),
            _ => query.OrderBy(e => e.EventStartDate >= utcNow ? 0 : 1)
                      .ThenBy(e => e.EventStartDate)
        };

        // Server-Side Count & Pagination
        var totalCount = await query.CountAsync();

        var items = await query
            .Include(e => e.Department)
            .Include(e => e.EventCategory)
            .Include(e => e.Sessions)
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

    /// <inheritdoc />
    public async Task<Event?> GetPublishedEventDetailsAsync(int eventId)
    {
        // Enforce Server-Side Visibility: If non-published or deleted, return null (404)
        var evt = await _context.Events
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.EventCategory)
            .Include(e => e.Sessions)
            .Include(e => e.Eligibilities)
                .ThenInclude(el => el.Department)
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted && e.Status == EventStatus.Published);

        if (evt == null)
            return null;

        // Ensure chronological order of sessions
        evt.Sessions = evt.Sessions
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ToList();

        // Protect internal governance audit data from student exposure
        evt.RejectionReason = null;
        evt.ApprovedByUserId = null;
        evt.ApprovedByUser = null;

        return evt;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryWithCountDto>> GetActiveCategoriesWithCountAsync()
    {
        var categories = await _context.EventCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var activePublishedCounts = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.Status == EventStatus.Published)
            .GroupBy(e => e.EventCategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        return categories.Select(c => new CategoryWithCountDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            IconClass = c.IconClass,
            PublishedCount = activePublishedCounts.TryGetValue(c.Id, out var count) ? count : 0
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Department>> GetActiveDepartmentsAsync()
    {
        return await _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<StudentEventEligibilityResult> EvaluateStudentEligibilityAsync(int eventId, string studentUserId)
    {
        var result = new StudentEventEligibilityResult();

        var student = await _userManager.FindByIdAsync(studentUserId);
        var eligibilities = await _context.EventEligibilities
            .AsNoTracking()
            .Include(el => el.Department)
            .Where(el => el.EventId == eventId)
            .ToListAsync();

        if (!eligibilities.Any())
        {
            result.IsEligible = true;
            result.Status = "Open to All";
            result.Message = "This event is open to all university students across all departments and academic years.";
            result.HasRestrictions = false;
            return result;
        }

        result.HasRestrictions = true;
        var rules = new List<string>();
        bool isEligible = true;

        // 1. Department Eligibility Check
        var deptRestrictedRules = eligibilities.Where(el => el.DepartmentId.HasValue).ToList();
        if (deptRestrictedRules.Any())
        {
            var allowedDeptNames = deptRestrictedRules
                .Select(el => el.Department?.Name ?? $"Dept #{el.DepartmentId}")
                .Distinct()
                .ToList();

            var allowedDeptCodes = deptRestrictedRules
                .Select(el => el.Department?.Code)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            result.EligibleDepartmentsDescription = string.Join(", ", allowedDeptNames);
            rules.Add($"Restricted to Departments: {result.EligibleDepartmentsDescription}");

            if (student != null && !string.IsNullOrWhiteSpace(student.Department))
            {
                var studentDeptNormalized = student.Department.Trim().ToUpperInvariant();
                var deptMatch = allowedDeptNames.Any(d => d.Trim().ToUpperInvariant() == studentDeptNormalized)
                    || allowedDeptCodes.Any(c => c!.Trim().ToUpperInvariant() == studentDeptNormalized);

                if (!deptMatch)
                {
                    isEligible = false;
                    result.Message = $"This event is restricted to students from: {result.EligibleDepartmentsDescription}. Your registered department is '{student.Department}'.";
                }
            }
        }
        else
        {
            result.EligibleDepartmentsDescription = "All Departments";
        }

        // 2. Year of Study Check
        var yearRules = eligibilities.Where(el => el.YearOfStudy.HasValue).Select(el => el.YearOfStudy!.Value).Distinct().ToList();
        if (yearRules.Any())
        {
            result.YearOfStudyDescription = $"Year(s): {string.Join(", ", yearRules)}";
            rules.Add($"Eligible Academic Years: {result.YearOfStudyDescription}");

            if (student?.YearOfStudy != null)
            {
                if (!yearRules.Contains(student.YearOfStudy.Value))
                {
                    isEligible = false;
                    result.Message = $"This event is restricted to academic year(s): {string.Join(", ", yearRules)}. You are in Year {student.YearOfStudy.Value}.";
                }
            }
        }
        else
        {
            result.YearOfStudyDescription = "All Years of Study";
        }

        // 3. Inter-College Scope
        var externalCollegeRules = eligibilities.Where(el => !el.IsOpenToOtherColleges).ToList();
        if (externalCollegeRules.Any())
        {
            result.CollegeScopeDescription = "VAP UNIVERSITY Students Only (External colleges not permitted)";
            rules.Add(result.CollegeScopeDescription);

            if (student != null && !string.IsNullOrWhiteSpace(student.CollegeName))
            {
                if (!student.CollegeName.Trim().Equals("VAP UNIVERSITY", StringComparison.OrdinalIgnoreCase))
                {
                    isEligible = false;
                    result.Message = "This event is exclusively reserved for internal VAP UNIVERSITY students.";
                }
            }
        }
        else
        {
            result.CollegeScopeDescription = "Open to VAP UNIVERSITY and External Colleges";
        }

        // 4. CGPA Requirement
        var minCgpa = eligibilities.Where(el => el.MinimumCgpa.HasValue).Select(el => el.MinimumCgpa!.Value).FirstOrDefault();
        if (minCgpa > 0)
        {
            result.MinimumCgpaDescription = $"Minimum CGPA: {minCgpa:F2}";
            rules.Add(result.MinimumCgpaDescription);
        }

        // 5. Special Remarks
        var remarks = eligibilities.Where(el => !string.IsNullOrWhiteSpace(el.Remarks)).Select(el => el.Remarks!.Trim()).Distinct().ToList();
        if (remarks.Any())
        {
            result.SpecialRemarks = string.Join("; ", remarks);
            rules.Add($"Remarks: {result.SpecialRemarks}");
        }

        result.IsEligible = isEligible;
        result.CriteriaBreakdown = rules;

        if (isEligible)
        {
            result.Status = "Eligible";
            result.Message = "You meet all departmental and academic eligibility criteria for this event.";
        }
        else
        {
            result.Status = "Eligibility Restriction";
        }

        return result;
    }
}
