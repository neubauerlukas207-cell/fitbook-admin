using FitBook.Admin.Data;
using FitBook.Admin.Domain;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Services;

public class CheckInService(ApplicationDbContext db, BusinessClock clock)
{
    /// <summary>Opakované pípnutí kartou v této době se nepočítá jako nový vstup.</summary>
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(60);

    public async Task<ServiceResult<CheckIn>> CheckInAsync(int customerId)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return ServiceResult<CheckIn>.Fail("Zákazník neexistuje.");

        var now = clock.UtcNow;
        var recent = await db.CheckIns.AnyAsync(c => c.CustomerId == customerId && c.AtUtc > now - DuplicateWindow);
        if (recent) return ServiceResult<CheckIn>.Fail($"{customer.Name} už je uvnitř (vstup během poslední hodiny).");

        var today = clock.Today;
        var candidates = await db.Memberships
            .Include(m => m.Plan)
            .Where(m => m.CustomerId == customerId && m.ValidFrom <= today && m.ValidTo >= today)
            .ToListAsync();

        // přednost má neomezená permanentka, pak ta na vstupy, které nejdřív vyprší
        var membership = candidates
            .Where(m => m.IsValidOn(today))
            .OrderBy(m => m.RemainingEntries is null ? 0 : 1)
            .ThenBy(m => m.ValidTo)
            .FirstOrDefault();
        if (membership is null) return ServiceResult<CheckIn>.Fail($"{customer.Name} nemá platnou permanentku.");

        if (membership.RemainingEntries is not null) membership.RemainingEntries--;

        var checkIn = new CheckIn { CustomerId = customerId, MembershipId = membership.Id, AtUtc = now };
        db.CheckIns.Add(checkIn);
        await db.SaveChangesAsync();

        var info = membership.RemainingEntries is { } left
            ? $"zbývá {left} {Czech.Plural(left, "vstup", "vstupy", "vstupů")}"
            : $"platí do {membership.ValidTo:d. M. yyyy}";
        return ServiceResult<CheckIn>.Ok(checkIn, $"Vítejte, {customer.Name}! ({membership.Plan.Name}, {info})");
    }
}

public static class Czech
{
    /// <summary>Česká množná čísla: 1 vstup, 2–4 vstupy, 0 a 5+ vstupů.</summary>
    public static string Plural(int n, string one, string few, string many) => n switch
    {
        1 => one,
        >= 2 and <= 4 => few,
        _ => many,
    };
}
