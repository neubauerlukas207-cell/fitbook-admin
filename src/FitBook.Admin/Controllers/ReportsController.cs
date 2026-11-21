using FitBook.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitBook.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ReportsController(ReportService reports) : Controller
{
    public async Task<IActionResult> Index() => View(await reports.RevenueByMonthAsync(6));
}
