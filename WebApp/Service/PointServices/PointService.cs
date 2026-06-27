using System.Net.Http.Json;
using Core;
using WebApp.Service.ApiErrors;

namespace WebApp.Service.PointServices;

/// <summary>
/// HTTP-baseret implementering af IPointService.
/// 
/// Bemærk:
/// - Endpoints matcher PointController i backend.
/// - Returnerer aldrig null-lister (giver tom liste i stedet) for at undgå null-checks i UI.
/// </summary>
public class PointService : IPointService
{
    private readonly HttpClient _http;

    // Saml routes ét sted, så de er nemme at ændre (hvis du en dag ændrer controller routes).
    private const string BaseRoute = "api/point";
    private const string DeleteAllRoute = "api/point/all";

    public PointService(HttpClient http)
    {
        _http = http;
    }

    /// <inheritdoc />
    public async Task<List<PointEntry>> GetAll()
    {
        // Returnér altid en liste (aldrig null), så UI er simpelt.
        return await _http.GetFromJsonAsync<List<PointEntry>>(BaseRoute) ?? new();
    }

    /// <inheritdoc />
    public async Task Add(PointEntry point)
    {
        // Vi forventer ikke noget svar-body.
        var res = await _http.PostAsJsonAsync(BaseRoute, ToCreateRequest(point));
        await res.EnsureSuccessWithApiMessageAsync();
    }

    /// <inheritdoc />
    public async Task Delete(int id)
    {
        var res = await _http.DeleteAsync($"{BaseRoute}/{id}");
        await res.EnsureSuccessWithApiMessageAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAll()
    {
        var res = await _http.DeleteAsync(DeleteAllRoute);
        await res.EnsureSuccessWithApiMessageAsync();
    }

    private static CreatePointRequest ToCreateRequest(PointEntry point) => new()
    {
        PlayerId = point.PlayerId,
        Points = point.Points,
        Date = point.Date
    };
}
