using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

/// <summary>
/// Institutional payment ledger and financial oversight controller for Administrators and SuperAdmins.
/// </summary>
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
public class PaymentsController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService paymentService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Admin/Payments
    /// Master institutional payment dashboard with cross-event search and filtering.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PaymentFilterCriteria criteria)
    {
        criteria ??= new PaymentFilterCriteria();

        var events = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Title, e.EventType, e.RegistrationFee })
            .ToListAsync();

        var departments = await _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Code })
            .ToListAsync();

        ViewBag.EventsList = events
            .Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = $"{e.Title} ({(e.EventType == EventType.Free ? "Free" : $"Fee: INR {e.RegistrationFee:F2}")})",
                Selected = criteria.EventId.HasValue && criteria.EventId.Value == e.Id
            })
            .ToList();

        ViewBag.DepartmentsList = departments
            .Select(d => new SelectListItem
            {
                Value = d.Id.ToString(),
                Text = $"{d.Name} ({d.Code})",
                Selected = criteria.DepartmentId.HasValue && criteria.DepartmentId.Value == d.Id
            })
            .ToList();

        var pagedResult = await _paymentService.GetPaymentsForAdminAsync(criteria);

        ViewBag.Criteria = criteria;
        return View(pagedResult);
    }

    /// <summary>
    /// GET: /Admin/Payments/Details/{id}
    /// Full transaction metadata inspection.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var role = User.IsInRole(nameof(UserRole.SuperAdmin)) ? nameof(UserRole.SuperAdmin) : nameof(UserRole.Admin);
        var payment = await _paymentService.GetPaymentDetailsForStaffAsync(id, currentUser.Id, role);

        if (payment == null) return NotFound();

        return View(payment);
    }
}
