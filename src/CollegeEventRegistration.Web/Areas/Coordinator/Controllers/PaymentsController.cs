using System;
using System.Collections.Generic;
using System.Linq;
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

namespace CollegeEventRegistration.Web.Areas.Coordinator.Controllers;

/// <summary>
/// Faculty Coordinator payment records overview.
/// Strictly scoped to events coordinated by the authenticated faculty member.
/// </summary>
[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.SuperAdmin)}")]
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
    /// GET: /Coordinator/Payments
    /// Lists payment transactions for an authorized event coordinated by the faculty member.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PaymentFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        criteria ??= new PaymentFilterCriteria();

        // Retrieve coordinated events
        var coordinatedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted && (e.CreatedByUserId == currentUser.Id || e.Coordinators.Any(c => c.UserId == currentUser.Id)))
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new { e.Id, e.Title, e.EventType, e.RegistrationFee })
            .ToListAsync();

        ViewBag.CoordinatedEvents = coordinatedEvents
            .Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = $"{e.Title} ({(e.EventType == EventType.Free ? "Free" : $"Fee: INR {e.RegistrationFee:F2}")})",
                Selected = criteria.EventId.HasValue && criteria.EventId.Value == e.Id
            })
            .ToList();

        if (!criteria.EventId.HasValue && coordinatedEvents.Any())
        {
            criteria.EventId = coordinatedEvents.First().Id;
        }

        PagedResult<PaymentSummaryDto> result;

        if (criteria.EventId.HasValue && criteria.EventId.Value > 0)
        {
            try
            {
                result = await _paymentService.GetPaymentsForCoordinatorAsync(
                    criteria.EventId.Value,
                    currentUser.Id,
                    criteria);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized payment record access by Coordinator {UserId}", currentUser.Id);
                TempData["ErrorMessage"] = "You are not authorized to view payments for that event.";
                result = new PagedResult<PaymentSummaryDto> { Items = new List<PaymentSummaryDto>() };
            }
        }
        else
        {
            result = new PagedResult<PaymentSummaryDto> { Items = new List<PaymentSummaryDto>() };
        }

        ViewBag.Criteria = criteria;
        return View(result);
    }
}
