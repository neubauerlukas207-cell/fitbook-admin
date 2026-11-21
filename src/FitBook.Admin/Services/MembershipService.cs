using FitBook.Admin.Data;
using FitBook.Admin.Domain;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Services;

public record ServiceResult<T>(bool Success, string Message, T? Value = default)
{
    public static ServiceResult<T> Ok(T value, string message) => new(true, message, value);
    public static ServiceResult<T> Fail(string message) => new(false, message);
}

public class MembershipService(ApplicationDbContext db, BusinessClock clock)
{
    /// <summary>
    /// Prodá permanentku. Neomezená permanentka navazuje na stávající platnou (prodloužení),
    /// takže zákazník nepřijde o zaplacené dny.
    /// </summary>
    public async Task<ServiceResult<Membership>> SellAsync(int customerId, int planId, DateOnly? startDate = null)
    {
        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null) return ServiceResult<Membership>.Fail("Zákazník neexistuje.");

        var plan = await db.Plans.FindAsync(planId);
        if (plan is null || !plan.IsActive) return ServiceResult<Membership>.Fail("Permanentka není v prodeji.");

        var today = clock.Today;
        var start = startDate ?? today;
        if (start < today) return ServiceResult<Membership>.Fail("Platnost nemůže začít v minulosti.");

        if (plan.Kind == PlanKind.Unlimited)
        {
            var lastUnlimitedEnd = await db.Memberships
                .Where(m => m.CustomerId == customerId && m.Plan.Kind == PlanKind.Unlimited && m.ValidTo >= start)
                .MaxAsync(m => (DateOnly?)m.ValidTo);
            if (lastUnlimitedEnd is { } end) start = end.AddDays(1);
        }

        var membership = new Membership
        {
            CustomerId = customerId,
            PlanId = plan.Id,
            ValidFrom = start,
            ValidTo = start.AddDays(plan.ValidityDays - 1),
            RemainingEntries = plan.Kind == PlanKind.Entries ? plan.Entries : null,
            PricePaid = plan.Price,
            SoldAtUtc = clock.UtcNow,
        };
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();

        var message = start > today
            ? $"Prodáno: {plan.Name}, platnost navazuje od {start:d. M. yyyy}."
            : $"Prodáno: {plan.Name}, platí do {membership.ValidTo:d. M. yyyy}.";
        return ServiceResult<Membership>.Ok(membership, message);
    }
}
