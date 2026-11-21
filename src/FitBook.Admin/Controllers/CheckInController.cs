using FitBook.Admin.Data;
using FitBook.Admin.Models;
using FitBook.Admin.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Controllers;

/// <summary>Recepce: vyhledání zákazníka a odbavení vstupu.</summary>
public class CheckInController(ApplicationDbContext db, CheckInService checkIns, BusinessClock clock) : Controller
{
    public async Task<IActionResult> Index(string? q)
    {
        var results = string.IsNullOrWhiteSpace(q) ? [] : await CustomerQueries.SearchAsync(db, q, clock.Today, take: 10);
        var recent = await db.CheckIns
            .Include(c => c.Customer)
            .Where(c => c.AtUtc >= clock.StartOfDayUtc(clock.Today))
            .OrderByDescending(c => c.AtUtc)
            .Take(15)
            .ToListAsync();
        return View(new CheckInViewModel(q, results, recent));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int customerId)
    {
        var result = await checkIns.CheckInAsync(customerId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
