namespace FitBook.Admin.Services;

/// <summary>Lekce tak, jak je vrací Java služba FitBook API (GET /api/lessons).</summary>
public record LessonDto(long Id, string Type, DateTime StartsAt, string Room, string Instructor,
    int Capacity, int Booked, int FreeSpots)
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["SPINNING"] = "Spinning", ["CROSSFIT"] = "CrossFit", ["BODY_PUMP"] = "Body pump", ["ZUMBA"] = "Zumba",
        ["KRUHOVY_TRENINK"] = "Kruhový trénink", ["JOGA"] = "Jóga", ["PILATES"] = "Pilates",
    };

    public string TypeName => Names.GetValueOrDefault(Type, Type);
}

/// <summary>
/// Typovaný HttpClient na FitBook API. Když API neběží, vrací null – administrace funguje dál,
/// jen bez rozvrhu lekcí.
/// </summary>
public class FitBookApiClient(HttpClient http, ILogger<FitBookApiClient> log)
{
    public async Task<IReadOnlyList<LessonDto>?> GetTodayLessonsAsync(DateOnly today, CancellationToken ct = default)
    {
        if (http.BaseAddress is null) return null;
        try
        {
            var day = today.ToString("yyyy-MM-dd");
            return await http.GetFromJsonAsync<List<LessonDto>>($"/api/lessons?from={day}&to={day}", ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            log.LogWarning("FitBook API není dostupné: {Message}", e.Message);
            return null;
        }
    }
}
