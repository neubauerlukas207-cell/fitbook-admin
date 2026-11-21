using FitBook.Admin.Domain;
using FitBook.Admin.Services;

namespace FitBook.Admin.Tests;

public class ReportServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Trzby_po_mesicich()
    {
        var sales = new MembershipService(_t.Db, _t.Clock);
        var plan = _t.Plan(PlanKind.Entries, 30, entries: 1, price: 150);
        await sales.SellAsync(_t.Customer().Id, plan.Id);
        await sales.SellAsync(_t.Customer().Id, plan.Id);
        _t.Time.Advance(TimeSpan.FromDays(15)); // 6. 12.
        await sales.SellAsync(_t.Customer().Id, plan.Id);

        var months = await new ReportService(_t.Db, _t.Clock).RevenueByMonthAsync(3);

        Assert.Equal(3, months.Count);
        Assert.Equal((2026, 10, 0m), (months[0].Year, months[0].Month, months[0].Revenue));
        Assert.Equal((2026, 11, 300m, 2), (months[1].Year, months[1].Month, months[1].Revenue, months[1].Sold));
        Assert.Equal((2026, 12, 150m, 1), (months[2].Year, months[2].Month, months[2].Revenue, months[2].Sold));
    }

    [Fact]
    public async Task Prehled_pocita_aktivni_cleny_a_dnesni_vstupy()
    {
        var sales = new MembershipService(_t.Db, _t.Clock);
        var checkIns = new CheckInService(_t.Db, _t.Clock);
        var plan = _t.Plan(PlanKind.Unlimited, 5, price: 500);
        var a = _t.Customer("A");
        var b = _t.Customer("B");
        _t.Customer("bez permanentky");
        await sales.SellAsync(a.Id, plan.Id);
        await sales.SellAsync(b.Id, plan.Id);
        await checkIns.CheckInAsync(a.Id);

        var stats = await new ReportService(_t.Db, _t.Clock).DashboardAsync();

        Assert.Equal(2, stats.ActiveMembers);
        Assert.Equal(1, stats.CheckInsToday);
        Assert.Equal(1000m, stats.RevenueThisMonth);
        Assert.Equal(1, stats.CheckInsByHour[10]); // 9:00 UTC = 10:00 v Praze
        Assert.Equal(2, stats.ExpiringSoon.Count);  // pětidenní permanentky vyprší do týdne
    }
}
