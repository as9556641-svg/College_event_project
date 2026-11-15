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
/// Coordinator ticket management controller.
/// Provides scoped inspection of issued tickets for assigned events.
/// Strictly enforces event ownership boundaries.
/// </summary>
[Area("Coordinator")]
[Authorize(Roles = $"{nameof(UserRole.Coordinator)},{nameof(UserRole.SuperAdmin)}")]
public class TicketsController : Controller
{
    private readonly ITicketService _ticketService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(
        ITicketService ticketService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Coordinator/Tickets?eventId={eventId}
    /// Lists all issued admission tickets for an event assigned to the coordinator.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int eventId,
        [FromQuery] TicketFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        criteria ??= new TicketFilterCriteria();

        // Retrieve coordinated events
        var coordinatedEvents = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted && (e.CreatedByUserId == currentUser.Id || e.Coordinators.Any(c => c.UserId == currentUser.Id)))
            .OrderByDescending(e => e.EventStartDate)
            .Select(e => new { e.Id, e.Title, e.EventType })
            .ToListAsync();

        if (!coordinatedEvents.Any())
        {
            ViewBag.CoordinatedEvents = new List<SelectListItem>();
            return View(new PagedResult<TicketDto> { Items = new List<TicketDto>() });
        }

        if (eventId > 0)
        {
            criteria.EventId = eventId;
        }
        else if (!criteria.EventId.HasValue || criteria.EventId.Value <= 0)
        {
            criteria.EventId = coordinatedEvents.First().Id;
        }

        ViewBag.CoordinatedEvents = coordinatedEvents
            .Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = e.Title,
                Selected = criteria.EventId.HasValue && criteria.EventId.Value == e.Id
            })
            .ToList();

        var selectedEvent = coordinatedEvents.FirstOrDefault(e => e.Id == criteria.EventId.Value);
        ViewBag.EventTitle = selectedEvent?.Title ?? $"Event #{criteria.EventId}";
        ViewBag.EventId = criteria.EventId.Value;
        ViewBag.Criteria = criteria;

        try
        {
            var pagedResult = await _ticketService.GetTicketsForCoordinatorAsync(
                criteria.EventId.Value,
                currentUser.Id,
                criteria);

            return View(pagedResult);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Coordinator {UserId} denied access to tickets for Event #{EventId}",
                currentUser.Id, criteria.EventId);
            TempData["ErrorMessage"] = "You are not authorized to view tickets for this event.";
            return RedirectToAction("Index", "Events");
        }
    }

    /// <summary>
    /// GET: /Coordinator/Tickets/Details/{id}
    /// Detailed inspection view for a single admission ticket.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var ticket = await _ticketService.GetTicketDetailsForStaffAsync(
            id,
            currentUser.Id,
            nameof(UserRole.Coordinator));

        if (ticket == null)
        {
            TempData["ErrorMessage"] = "Ticket not found or you are not authorized to inspect this ticket.";
            return RedirectToAction(nameof(Index));
        }

        return View(ticket);
    }
}
