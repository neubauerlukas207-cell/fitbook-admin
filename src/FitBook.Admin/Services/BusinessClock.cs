namespace FitBook.Admin.Services;

/// <summary>
/// Čas fitka v pražském čase. Staví na <see cref="TimeProvider"/> (.NET 8), takže v testech jde
/// „posunout hodiny“ bez čekání a bez závislosti na skutečném čase.
/// </summary>
public class BusinessClock(TimeProvider time)
{
    private static readonly TimeZoneInfo Prague = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, Prague);

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Prague);

    /// <summary>Začátek daného dne (00:00 v Praze) převedený na UTC – pro dotazy do databáze.</summary>
    public DateTime StartOfDayUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Prague);
}
