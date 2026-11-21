using FitBook.Admin.Domain;
using FitBook.Admin.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Data;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Reception = "Recepce";
}

public static class SeedData
{
    /// <summary>Role a ceník – potřeba vždy.</summary>
    public static async Task EnsureBaseDataAsync(IServiceProvider services)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.Reception })
        {
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
        }

        var db = services.GetRequiredService<ApplicationDbContext>();
        if (!await db.Plans.AnyAsync())
        {
            db.Plans.AddRange(
                new MembershipPlan { Name = "Měsíční neomezená", Kind = PlanKind.Unlimited, ValidityDays = 30, Price = 990 },
                new MembershipPlan { Name = "Roční neomezená", Kind = PlanKind.Unlimited, ValidityDays = 365, Price = 8990 },
                new MembershipPlan { Name = "10 vstupů", Kind = PlanKind.Entries, ValidityDays = 180, Entries = 10, Price = 1290 },
                new MembershipPlan { Name = "Jednorázový vstup", Kind = PlanKind.Entries, ValidityDays = 1, Entries = 1, Price = 150 });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>První administrátor pro produkci – e-mail a heslo z proměnných prostředí, nikdy z kódu.</summary>
    public static async Task EnsureBootstrapAdminAsync(IServiceProvider services, string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        await EnsureUserAsync(services.GetRequiredService<UserManager<IdentityUser>>(), email, password, Roles.Admin);
    }

    /// <summary>Ukázkoví uživatelé a zákazníci – jen pro vývoj a demo (heslo z konfigurace).</summary>
    public static async Task EnsureDemoDataAsync(IServiceProvider services, string password)
    {
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        await EnsureUserAsync(users, "admin@fitbook.local", password, Roles.Admin);
        await EnsureUserAsync(users, "recepce@fitbook.local", password, Roles.Reception);

        var db = services.GetRequiredService<ApplicationDbContext>();
        if (await db.Customers.AnyAsync()) return;

        var clock = services.GetRequiredService<BusinessClock>();
        var sales = services.GetRequiredService<MembershipService>();
        var monthly = await db.Plans.FirstAsync(p => p.Kind == PlanKind.Unlimited && p.ValidityDays == 30);
        var today = clock.Today;
        string[] names = ["Eva Nováková", "Karel Horák", "Lucie Černá", "Martin Veselý", "Tereza Králová", "Ondřej Pokorný"];
        int[] visitHours = [6, 7, 7, 8, 12, 16, 17, 17, 18, 18, 19, 20];

        for (var i = 0; i < names.Length; i++)
        {
            var customer = new Customer
            {
                Name = names[i],
                Email = $"zakaznik{i + 1}@example.com",
                CreatedAtUtc = clock.UtcNow.AddDays(-180 + i * 10),
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            // historie: měsíční permanentky z minulých měsíců (kvůli přehledu tržeb)
            for (var k = i % 5 + 1; k >= 1; k--)
            {
                var from = today.AddMonths(-k).AddDays(-i);
                db.Memberships.Add(new Membership
                {
                    CustomerId = customer.Id, PlanId = monthly.Id, ValidFrom = from, ValidTo = from.AddDays(29),
                    PricePaid = monthly.Price, SoldAtUtc = clock.StartOfDayUtc(from).AddHours(9),
                });
            }
            await db.SaveChangesAsync();

            var sale = await sales.SellAsync(customer.Id, (i % 3) + 1);

            // dnešní vstupy (jen ty, které už proběhly)
            foreach (var hour in visitHours.Where((_, idx) => idx % names.Length == i || idx % 4 == i))
            {
                var at = clock.StartOfDayUtc(today).AddHours(hour).AddMinutes(7 * i);
                if (at < clock.UtcNow && sale.Value is { } m)
                    db.CheckIns.Add(new CheckIn { CustomerId = customer.Id, MembershipId = m.Id, AtUtc = at });
            }
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureUserAsync(UserManager<IdentityUser> users, string email, string password, string role)
    {
        if (await users.FindByEmailAsync(email) is not null) return;
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
    }
}
