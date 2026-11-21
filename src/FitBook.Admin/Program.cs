using System.Text.Encodings.Web;
using System.Text.Unicode;
using FitBook.Admin.Data;
using FitBook.Admin.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chybí connection string 'DefaultConnection'.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Přihlašování zaměstnanců (ASP.NET Core Identity) s rolemi Admin a Recepce
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 10;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin))
    // vše je ve výchozím stavu jen pro přihlášené; veřejné stránky mají [AllowAnonymous]
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
// čeština v HTML bez escapování na &#x...; (Razor jinak kóduje vše mimo ASCII)
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<BusinessClock>();
builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<CheckInService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddHttpClient<FitBookApiClient>(http =>
{
    var url = builder.Configuration["FitBookApi:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(url)) http.BaseAddress = new Uri(url);
    http.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Databáze: migrace + základní data (role, ceník), volitelně demo data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (db.Database.IsRelational()) await db.Database.MigrateAsync();
    await SeedData.EnsureBaseDataAsync(scope.ServiceProvider);
    await SeedData.EnsureBootstrapAdminAsync(scope.ServiceProvider,
        app.Configuration["Bootstrap:AdminEmail"], app.Configuration["Bootstrap:AdminPassword"]);
    var demoPassword = app.Configuration["Demo:Password"];
    if (!string.IsNullOrEmpty(demoPassword)) await SeedData.EnsureDemoDataAsync(scope.ServiceProvider, demoPassword);
}

if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection")) app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();

// Účty zaměstnanců zakládá jen admin. Stránka Register z Identity UI má [AllowAnonymous],
// které by přebilo autorizační pravidla, proto ji hlídáme už tady.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Identity/Account/Register") && !context.User.IsInRole(Roles.Admin))
    {
        await context.ForbidAsync();
        return;
    }
    await next();
});

app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

/// <summary>Zpřístupní třídu Program integračním testům (WebApplicationFactory).</summary>
public partial class Program;
