using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CollegeEventRegistration.Web.Models;

namespace CollegeEventRegistration.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
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
