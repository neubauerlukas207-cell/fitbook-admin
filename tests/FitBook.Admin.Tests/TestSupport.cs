using FitBook.Admin.Data;
using FitBook.Admin.Domain;
using FitBook.Admin.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Tests;

/// <summary>Hodiny, které jdou v testu posouvat – nic se nečeká, nic nezávisí na skutečném čase.</summary>
public class FakeTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>
/// Skutečná relační databáze (SQLite) v paměti – na rozdíl od EF InMemory providera kontroluje
/// cizí klíče, unikátní indexy a překládá dotazy do SQL stejně jako v produkci.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    // 21. 11. 2026 10:00 v Praze (UTC+1)
    public FakeTime Time { get; } = new(new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero));
    public ApplicationDbContext Db { get; }
    public BusinessClock Clock { get; }

    public TestDb()
    {
        _connection.Open();
        Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();
        Clock = new BusinessClock(Time);
    }

    public MembershipPlan Plan(PlanKind kind, int days, int? entries = null, decimal price = 1000, bool active = true)
    {
        var plan = new MembershipPlan { Name = $"{kind} {days}", Kind = kind, ValidityDays = days, Entries = entries, Price = price, IsActive = active };
        Db.Plans.Add(plan);
        Db.SaveChanges();
        return plan;
    }

    public Customer Customer(string name = "Eva Nováková")
    {
        var customer = new Customer { Name = name, Email = $"{Guid.NewGuid():N}@example.com", CreatedAtUtc = Clock.UtcNow };
        Db.Customers.Add(customer);
        Db.SaveChanges();
        return customer;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
