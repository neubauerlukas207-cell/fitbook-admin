using FitBook.Admin.Domain;
using FitBook.Admin.Services;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Tests;

public class CheckInServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly CheckInService _checkIns;
    private readonly MembershipService _sales;

    public CheckInServiceTests()
    {
        _checkIns = new CheckInService(_t.Db, _t.Clock);
        _sales = new MembershipService(_t.Db, _t.Clock);
    }

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Bez_permanentky_neprojde()
    {
        var result = await _checkIns.CheckInAsync(_t.Customer().Id);

        Assert.False(result.Success);
        Assert.Contains("nemá platnou permanentku", result.Message);
    }

    [Fact]
    public async Task S_neomezenou_permanentkou_projde()
    {
        var customer = _t.Customer();
        await _sales.SellAsync(customer.Id, _t.Plan(PlanKind.Unlimited, 30).Id);

        var result = await _checkIns.CheckInAsync(customer.Id);

        Assert.True(result.Success);
        Assert.Equal(1, await _t.Db.CheckIns.CountAsync());
    }

    [Fact]
    public async Task Vstup_odecte_z_permanentky_a_po_vycerpani_neprojde()
    {
        var customer = _t.Customer();
        var sale = await _sales.SellAsync(customer.Id, _t.Plan(PlanKind.Entries, 180, entries: 2).Id);

        Assert.True((await _checkIns.CheckInAsync(customer.Id)).Success);
        _t.Time.Advance(TimeSpan.FromDays(1));
        var second = await _checkIns.CheckInAsync(customer.Id);
        _t.Time.Advance(TimeSpan.FromDays(1));
        var third = await _checkIns.CheckInAsync(customer.Id);

        Assert.True(second.Success);
        Assert.Contains("zbývá 0 vstupů", second.Message);
        Assert.False(third.Success);
        Assert.Equal(0, (await _t.Db.Memberships.FindAsync(sale.Value!.Id))!.RemainingEntries);
    }

    [Fact]
    public async Task Opakovane_pipnuti_do_hodiny_se_nepocita()
    {
        var customer = _t.Customer();
        await _sales.SellAsync(customer.Id, _t.Plan(PlanKind.Entries, 180, entries: 10).Id);
        await _checkIns.CheckInAsync(customer.Id);

        _t.Time.Advance(TimeSpan.FromMinutes(30));
        var duplicate = await _checkIns.CheckInAsync(customer.Id);
        _t.Time.Advance(TimeSpan.FromMinutes(31));
        var later = await _checkIns.CheckInAsync(customer.Id);

        Assert.False(duplicate.Success);
        Assert.Contains("už je uvnitř", duplicate.Message);
        Assert.True(later.Success);
        Assert.Contains("zbývá 8 vstupů", later.Message);
    }

    [Fact]
    public async Task Prednost_ma_neomezena_permanentka_vstupy_se_neodectou()
    {
        var customer = _t.Customer();
        var entries = await _sales.SellAsync(customer.Id, _t.Plan(PlanKind.Entries, 180, entries: 10).Id);
        await _sales.SellAsync(customer.Id, _t.Plan(PlanKind.Unlimited, 30).Id);

        await _checkIns.CheckInAsync(customer.Id);

        Assert.Equal(10, (await _t.Db.Memberships.FindAsync(entries.Value!.Id))!.RemainingEntries);
    }

    [Fact]
    public async Task Prosla_permanentka_neplati()
    {
        var customer = _t.Customer();
        await _sales.SellAsync(customer.Id, _t.Plan(PlanKind.Unlimited, 30).Id);

        _t.Time.Advance(TimeSpan.FromDays(30));

        Assert.False((await _checkIns.CheckInAsync(customer.Id)).Success);
    }

    [Theory]
    [InlineData(0, "vstupů")]
    [InlineData(1, "vstup")]
    [InlineData(2, "vstupy")]
    [InlineData(4, "vstupy")]
    [InlineData(5, "vstupů")]
    [InlineData(12, "vstupů")]
    public void Ceska_mnozna_cisla(int n, string expected) =>
        Assert.Equal(expected, Czech.Plural(n, "vstup", "vstupy", "vstupů"));
}
