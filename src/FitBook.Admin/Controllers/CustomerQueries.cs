using FitBook.Admin.Data;
using FitBook.Admin.Models;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Controllers;

internal static class CustomerQueries
{
    /// <summary>
    /// Seznam zákazníků s jejich aktuálně platnou permanentkou – jedním SQL dotazem (projekce do DTO),
    /// bez načítání celých entit.
    /// </summary>
    public static Task<List<CustomerRow>> SearchAsync(ApplicationDbContext db, string? query, DateOnly today, int take = 50)
    {
        var customers = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            customers = customers.Where(c => c.Name.ToLower().Contains(q) || c.Email.ToLower().Contains(q));
        }

        return customers
            .OrderBy(c => c.Name)
            .Take(take)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Email,
                Active = c.Memberships
                    .Where(m => m.ValidFrom <= today && m.ValidTo >= today && (m.RemainingEntries == null || m.RemainingEntries > 0))
                    .OrderByDescending(m => m.ValidTo)
                    .Select(m => new { m.Plan.Name, m.ValidTo, m.RemainingEntries })
                    .FirstOrDefault(),
            })
            .Select(x => new CustomerRow(x.Id, x.Name, x.Email,
                x.Active == null ? null : x.Active.Name,
                x.Active == null ? null : x.Active.ValidTo,
                x.Active == null ? null : x.Active.RemainingEntries))
            .ToListAsync();
    }
}
