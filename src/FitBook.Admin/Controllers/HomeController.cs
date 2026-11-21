using System.Diagnostics;
using FitBook.Admin.Models;
using FitBook.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitBook.Admin.Controllers;

public class HomeController(ReportService reports, FitBookApiClient api, BusinessClock clock) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var stats = await reports.DashboardAsync();
        var lessons = await api.GetTodayLessonsAsync(clock.Today, ct);
        return View(new DashboardViewModel(stats, lessons, clock.Today));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
