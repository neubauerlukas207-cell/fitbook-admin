using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using FitBook.Admin.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FitBook.Admin.Tests;

/// <summary>Spustí celou aplikaci v paměti s vlastní SQLite databází a testovacím přihlášením.</summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "DataSource=:memory:");
        builder.UseSetting("DisableHttpsRedirection", "true");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));

            // přihlášený uživatel se v testu určí hlavičkou X-Test-Role; bez ní je anonymní
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(o => o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName);
        });
    }

    public HttpClient ClientAs(string? role)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.Header, role);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}

public class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string Header = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var role)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, $"{role}@fitbook.test"), new Claim(ClaimTypes.Role, role!)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public class WebAppTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task Health_check_je_verejny()
    {
        var response = await factory.ClientAs(null).GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Customers")]
    [InlineData("/CheckIn")]
    public async Task Neprihlaseny_je_presmerovan_na_prihlaseni(string url)
    {
        var response = await factory.ClientAs(null).GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Plans")]
    [InlineData("/Reports")]
    [InlineData("/Identity/Account/Register")]
    public async Task Recepce_nema_pristup_do_administrace(string url)
    {
        var response = await factory.ClientAs(Roles.Reception).GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Admin_vidi_cenik_ze_seedu()
    {
        var html = await factory.ClientAs(Roles.Admin).GetStringAsync("/Plans");

        Assert.Contains("Měsíční neomezená", html);
        Assert.Contains("10 vstupů", html);
    }

    [Fact]
    public async Task Prehled_funguje_i_bez_FitBook_API()
    {
        var html = await factory.ClientAs(Roles.Reception).GetStringAsync("/");

        Assert.Contains("Aktivní členové", html);
        Assert.Contains("FitBook API není dostupné", html);
    }

    [Fact]
    public async Task Recepce_zalozi_zakaznika_proda_permanentku_a_odbavi_vstup()
    {
        var client = factory.ClientAs(Roles.Reception);

        // 1) nový zákazník přes formulář (včetně anti-forgery tokenu)
        var form = await client.GetStringAsync("/Customers/Create");
        var created = await client.PostAsync("/Customers/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Test Recepce",
            ["Email"] = "test.recepce@example.com",
            ["__RequestVerificationToken"] = Token(form),
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var detailUrl = created.Headers.Location!.ToString();
        var id = int.Parse(detailUrl.Split('/').Last());

        // 2) bez permanentky vstup neprojde
        var detail = await client.GetStringAsync(detailUrl);
        await client.PostAsync("/CheckIn/Create", Form(detail, ("customerId", id.ToString())));
        Assert.Contains("nemá platnou permanentku", await client.GetStringAsync("/CheckIn"));

        // 3) prodej permanentky na 10 vstupů a vstup
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var planId = db.Plans.Single(p => p.Name == "10 vstupů").Id;
            await client.PostAsync($"/Customers/Sell/{id}", Form(detail, ("planId", planId.ToString())));
        }
        await client.PostAsync("/CheckIn/Create", Form(detail, ("customerId", id.ToString())));
        var page = await client.GetStringAsync("/CheckIn");

        Assert.Contains("Vítejte, Test Recepce!", page);
        Assert.Contains("zbývá 9 vstupů", page);
    }

    [Fact]
    public async Task Formular_bez_anti_forgery_tokenu_je_odmitnut()
    {
        var response = await factory.ClientAs(Roles.Reception).PostAsync("/CheckIn/Create",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["customerId"] = "1" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private static FormUrlEncodedContent Form(string pageWithToken, params (string Key, string Value)[] fields)
    {
        var data = fields.ToDictionary(f => f.Key, f => f.Value);
        data["__RequestVerificationToken"] = Token(pageWithToken);
        return new FormUrlEncodedContent(data);
    }
}
