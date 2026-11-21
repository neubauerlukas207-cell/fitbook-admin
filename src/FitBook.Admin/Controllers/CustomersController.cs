using FitBook.Admin.Data;
using FitBook.Admin.Domain;
using FitBook.Admin.Models;
using FitBook.Admin.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Controllers;

public class CustomersController(ApplicationDbContext db, MembershipService memberships, BusinessClock clock) : Controller
{
    public async Task<IActionResult> Index(string? q) =>
        View(new CustomerListViewModel(await CustomerQueries.SearchAsync(db, q, clock.Today), q));

    public IActionResult Create() => View(new CustomerForm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerForm form)
    {
        var email = form.Email.Trim().ToLowerInvariant();
        if (await db.Customers.AnyAsync(c => c.Email == email))
            ModelState.AddModelError(nameof(form.Email), "Zákazník s tímto e-mailem už existuje");
        if (!ModelState.IsValid) return View(form);

        var customer = new Customer { Name = form.Name.Trim(), Email = email, Phone = form.Phone, CreatedAtUtc = clock.UtcNow };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Zákazník {customer.Name} byl přidán.";
        return RedirectToAction(nameof(Details), new { id = customer.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await db.Customers
            .Include(c => c.Memberships.OrderByDescending(m => m.ValidTo)).ThenInclude(m => m.Plan)
            .Include(c => c.CheckIns.OrderByDescending(ch => ch.AtUtc).Take(10))
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null) return NotFound();

        var plans = (await db.Plans.Where(p => p.IsActive).ToListAsync()).OrderBy(p => p.Price).ToList();
        return View(new CustomerDetailViewModel(customer, plans, clock.Today));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sell(int id, int planId)
    {
        var result = await memberships.SellAsync(id, planId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }
}
