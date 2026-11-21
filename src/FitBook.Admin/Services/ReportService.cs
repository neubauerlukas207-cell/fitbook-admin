using FitBook.Admin.Data;
using FitBook.Admin.Domain;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Services;

public record MonthRevenue(int Year, int Month, decimal Revenue, int Sold);

public record DashboardStats(int ActiveMembers, int CheckInsToday, decimal RevenueThisMonth,
    IReadOnlyList<Membership> ExpiringSoon, IReadOnlyList<int> CheckInsByHour);

public class ReportService(ApplicationDbContext db, BusinessClock clock)
{
    public async Task<DashboardStats> DashboardAsync()
    {
        var today = clock.Today;
        var dayStart = clock.StartOfDayUtc(today);
        var monthStart = clock.StartOfDayUtc(new DateOnly(today.Year, today.Month, 1));

        var activeMembers = await db.Memberships
            .Where(m => m.ValidFrom <= today && m.ValidTo >= today && (m.RemainingEntries == null || m.RemainingEntries > 0))
            .Select(m => m.CustomerId).Distinct().CountAsync();

        var checkInsToday = await db.CheckIns.Where(c => c.AtUtc >= dayStart).Select(c => c.AtUtc).ToListAsync();
        var byHour = new int[24];
        foreach (var at in checkInsToday) byHour[clock.ToLocal(at).Hour]++;

        // SQLite neumí SUM nad decimal – sčítáme v aplikaci (dat za měsíc je málo)
        var revenue = (await db.Memberships.Where(m => m.SoldAtUtc >= monthStart).Select(m => m.PricePaid).ToListAsync()).Sum();

        var expiring = await db.Memberships
            .Include(m => m.Customer).Include(m => m.Plan)
            .Where(m => m.ValidTo >= today && m.ValidTo <= today.AddDays(7))
            .OrderBy(m => m.ValidTo)
            .Take(10)
            .ToListAsync();

        return new DashboardStats(activeMembers, checkInsToday.Count, revenue, expiring, byHour);
    }

    public async Task<IReadOnlyList<MonthRevenue>> RevenueByMonthAsync(int months = 6)
    {
        var today = clock.Today;
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var sales = await db.Memberships
            .Where(m => m.SoldAtUtc >= clock.StartOfDayUtc(first))
            .Select(m => new { m.SoldAtUtc, m.PricePaid })
            .ToListAsync();

        return Enumerable.Range(0, months)
            .Select(i => first.AddMonths(i))
            .Select(month =>
            {
                var inMonth = sales.Where(s =>
                {
                    var local = clock.ToLocal(s.SoldAtUtc);
                    return local.Year == month.Year && local.Month == month.Month;
                }).ToList();
                return new MonthRevenue(month.Year, month.Month, inMonth.Sum(s => s.PricePaid), inMonth.Count);
            })
            .ToList();
    }
}
