using System.Diagnostics;
using CollegeEventRegistration.Core.Interfaces;
using CollegeEventRegistration.Web.Models;
using CollegeEventRegistration.Web.Models.Home;
using Microsoft.AspNetCore.Mvc;

namespace CollegeEventRegistration.Web.Controllers;

public class HomeController : Controller
{
    private readonly IEventDiscoveryService _discoveryService;

    public HomeController(IEventDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
    }

    public async Task<IActionResult> Index()
    {
        var viewModel = await HomePageAssembler.BuildAsync(_discoveryService);
        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCode(int? code)
    {
        var statusCode = code ?? 500;
        ViewBag.StatusCode = statusCode;
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

        return View("StatusCode", statusCode);
    }
}
