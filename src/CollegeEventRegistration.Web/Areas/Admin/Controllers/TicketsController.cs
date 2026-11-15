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

namespace CollegeEventRegistration.Web.Areas.Admin.Controllers;

/// <summary>
/// Master tickets ledger controller for campus administrators.
/// Provides campus-wide ticket inspection, audit oversight, and search.
/// </summary>
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.SuperAdmin)}")]
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
    /// GET: /Admin/Tickets
    /// Campus-wide master admission tickets ledger.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] TicketFilterCriteria criteria)
    {
        criteria ??= new TicketFilterCriteria();

        var tickets = await _ticketService.GetTicketsForAdminAsync(criteria);

        var events = await _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Title })
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
                Text = e.Title,
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

        ViewBag.Criteria = criteria;
        return View(tickets);
    }

    /// <summary>
    /// GET: /Admin/Tickets/Details/{id}
    /// Detailed administrative inspection view for a single ticket.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        var ticket = await _ticketService.GetTicketDetailsForStaffAsync(
            id,
            currentUser?.Id ?? string.Empty,
            nameof(UserRole.Admin));

        if (ticket == null)
        {
            TempData["ErrorMessage"] = "Ticket not found.";
            return RedirectToAction(nameof(Index));
        }

        return View(ticket);
    }
}
