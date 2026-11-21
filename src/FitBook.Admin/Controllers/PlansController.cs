using FitBook.Admin.Data;
using FitBook.Admin.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Controllers;

/// <summary>Ceník permanentek – jen pro administrátora.</summary>
[Authorize(Policy = "AdminOnly")]
public class PlansController(ApplicationDbContext db) : Controller
{
    // SQLite neumí ORDER BY nad decimal – ceník je malý, řadíme v paměti
    public async Task<IActionResult> Index() =>
        View((await db.Plans.AsNoTracking().ToListAsync())
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.Price).ToList());

    public IActionResult Create() => View("Edit", new MembershipPlan { ValidityDays = 30 });

    public async Task<IActionResult> Edit(int id) =>
        await db.Plans.FindAsync(id) is { } plan ? View(plan) : NotFound();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(MembershipPlan plan)
    {
        if (plan.Kind == PlanKind.Entries && plan.Entries is null)
            ModelState.AddModelError(nameof(plan.Entries), "Permanentka na vstupy musí mít počet vstupů");
        if (plan.Kind == PlanKind.Unlimited) plan.Entries = null;
        if (!ModelState.IsValid) return View("Edit", plan);

        if (plan.Id == 0) db.Plans.Add(plan);
        else db.Plans.Update(plan);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Permanentka „{plan.Name}“ uložena.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        if (await db.Plans.FindAsync(id) is not { } plan) return NotFound();
        plan.IsActive = !plan.IsActive;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
