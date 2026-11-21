using FitBook.Admin.Domain;
using FitBook.Admin.Services;

namespace FitBook.Admin.Tests;

public class MembershipServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly MembershipService _service;

    public MembershipServiceTests() => _service = new MembershipService(_t.Db, _t.Clock);

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Mesicni_permanentka_plati_od_dneska_30_dni()
    {
        var plan = _t.Plan(PlanKind.Unlimited, 30, price: 990);
        var customer = _t.Customer();

        var result = await _service.SellAsync(customer.Id, plan.Id);

        Assert.True(result.Success);
        Assert.Equal(new DateOnly(2026, 11, 21), result.Value!.ValidFrom);
        Assert.Equal(new DateOnly(2026, 12, 20), result.Value.ValidTo);
        Assert.Null(result.Value.RemainingEntries);
        Assert.Equal(990, result.Value.PricePaid);
    }

    [Fact]
    public async Task Druha_neomezena_permanentka_navazuje_na_prvni()
    {
        var plan = _t.Plan(PlanKind.Unlimited, 30);
        var customer = _t.Customer();
        await _service.SellAsync(customer.Id, plan.Id);

        var second = await _service.SellAsync(customer.Id, plan.Id);

        Assert.True(second.Success);
        Assert.Equal(new DateOnly(2026, 12, 21), second.Value!.ValidFrom);
        Assert.Contains("navazuje", second.Message);
    }

    [Fact]
    public async Task Permanentka_na_vstupy_nenavazuje_a_ma_pocet_vstupu()
    {
        var unlimited = _t.Plan(PlanKind.Unlimited, 30);
        var entries = _t.Plan(PlanKind.Entries, 180, entries: 10);
        var customer = _t.Customer();
        await _service.SellAsync(customer.Id, unlimited.Id);

        var result = await _service.SellAsync(customer.Id, entries.Id);

        Assert.Equal(_t.Clock.Today, result.Value!.ValidFrom);
        Assert.Equal(10, result.Value.RemainingEntries);
    }

    [Fact]
    public async Task Vyrazenou_permanentku_nelze_prodat()
    {
        var plan = _t.Plan(PlanKind.Unlimited, 30, active: false);

        var result = await _service.SellAsync(_t.Customer().Id, plan.Id);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Platnost_nemuze_zacit_v_minulosti()
    {
        var plan = _t.Plan(PlanKind.Unlimited, 30);

        var result = await _service.SellAsync(_t.Customer().Id, plan.Id, _t.Clock.Today.AddDays(-1));

        Assert.False(result.Success);
    }
}
