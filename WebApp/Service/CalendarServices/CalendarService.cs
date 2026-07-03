using System.Net.Http.Json;
using Core;
using WebApp.Service.ApiErrors;

namespace WebApp.Service.CalendarServices;

/// <summary>
/// HTTP-baseret implementering af ICalendarService.
/// 
/// Bemærk:
/// - Endpoints matcher CalendarController i backend.
/// - Returnerer aldrig null-lister (giver tom liste i stedet), så UI ikke skal null-checke.
/// - Vi ændrer ikke funktionalitet: samme routes, samme payloads.
/// </summary>
public class CalendarService : ICalendarService
{
    private readonly HttpClient _http;

    // Saml routes ét sted for bedre vedligehold.
    private const string BaseRoute = "api/calendar";

    public CalendarService(HttpClient http)
    {
        _http = http;
    }

    /// <inheritdoc />
    public async Task<List<Calendar>> GetAll()
    {
        return await _http.GetFromJsonAsync<List<Calendar>>(BaseRoute) ?? new();
    }

    /// <inheritdoc />
    public async Task Save(Calendar calendar)
    {
        // Backend håndterer add/update (opret/ret) på samme endpoint.
        var res = await _http.PostAsJsonAsync(BaseRoute, ToSaveRequest(calendar));
        await res.EnsureSuccessWithApiMessageAsync();
    }

    /// <inheritdoc />
    public async Task Delete(int id)
    {
        var res = await _http.DeleteAsync($"{BaseRoute}/{id}");
        await res.EnsureSuccessWithApiMessageAsync();
    }

    private static SaveCalendarRequest ToSaveRequest(Calendar calendar) => new()
    {
        Id = calendar.Id,
        Date = calendar.Date,
        Note = calendar.Note
    };
}
