using System.Threading.Tasks;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CollegeEventRegistration.Web.Areas.Student.Controllers;

/// <summary>
/// Student ticket controller managing digital admission passes,
/// cryptographic QR display, print passes, and strictly enforcing student tenant isolation.
/// </summary>
[Area("Student")]
[Authorize(Roles = $"{nameof(UserRole.Student)},{nameof(UserRole.SuperAdmin)}")]
public class TicketsController : Controller
{
    private readonly ITicketService _ticketService;
    private readonly IEventRegistrationService _registrationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(
        ITicketService ticketService,
        IEventRegistrationService registrationService,
        UserManager<ApplicationUser> userManager,
        ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _registrationService = registrationService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// GET: /Student/Tickets
    /// Lists all admission tickets for the authenticated student.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] TicketFilterCriteria criteria)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        criteria ??= new TicketFilterCriteria();
        var tickets = await _ticketService.GetStudentTicketsAsync(currentUser.Id, criteria);

        ViewBag.Criteria = criteria;
        return View(tickets);
    }

    /// <summary>
    /// GET: /Student/Tickets/Details/{id}
    /// Displays the digital admission ticket with high-contrast QR code and validity information.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (id <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Check if id is a ticket ID or registration ID
        var ticket = await _ticketService.GetOrGenerateTicketForStudentAsync(id, currentUser.Id);
        if (ticket == null)
        {
            // If not found by registration id, query by ticket id
            var staffTicket = await _ticketService.GetTicketDetailsForStaffAsync(id, currentUser.Id, nameof(UserRole.Admin));
            if (staffTicket != null && staffTicket.UserId == currentUser.Id)
            {
                ticket = staffTicket;
            }
        }

        if (ticket == null)
        {
            TempData["ErrorMessage"] = "Ticket not found or you are not authorized to view this ticket.";
            return RedirectToAction(nameof(Index));
        }

        return View(ticket);
    }

    /// <summary>
    /// GET: /Student/Tickets/ForRegistration?registrationId={registrationId}
    /// Retrieves or generates the admission ticket for a confirmed registration.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ForRegistration(int registrationId)
    {
        if (registrationId <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var ticket = await _ticketService.GetOrGenerateTicketForStudentAsync(registrationId, currentUser.Id);
        if (ticket == null)
        {
            TempData["ErrorMessage"] = "Ticket cannot be generated. Ensure your registration is confirmed and all required fees are paid.";
            return RedirectToAction("Details", "Registrations", new { id = registrationId });
        }

        return View("Details", ticket);
    }

    /// <summary>
    /// GET: /Student/Tickets/Print/{id}
    /// Official printable admission pass layout with university header and tear-off stub.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        if (id <= 0) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var ticket = await _ticketService.GetOrGenerateTicketForStudentAsync(id, currentUser.Id);
        if (ticket == null)
        {
            var staffTicket = await _ticketService.GetTicketDetailsForStaffAsync(id, currentUser.Id, nameof(UserRole.Admin));
            if (staffTicket != null && staffTicket.UserId == currentUser.Id)
            {
                ticket = staffTicket;
            }
        }

        if (ticket == null)
        {
            TempData["ErrorMessage"] = "Ticket not found or you are not authorized to print this ticket.";
            return RedirectToAction(nameof(Index));
        }

        return View(ticket);
    }
}
