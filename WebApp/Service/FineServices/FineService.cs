using Core;
using System.Net.Http.Json;
using WebApp.Service.ApiErrors;

namespace WebApp.Service.FineServices;

/// <summary>
/// Implementation af IFineService via HttpClient.
/// </summary>
public class FineService : IFineService
{
    private readonly HttpClient _http;

    /// <summary>
    /// HttpClient er typisk konfigureret i DI med BaseAddress mod din ServerAPI.
    /// </summary>
    public FineService(HttpClient http)
    {
        _http = http;
    }

    /// <inheritdoc />
    public async Task<Fine[]> GetAll()
    {
        // Henter alle bøder. Returnerer tomt array hvis API svarer null.
        return await _http.GetFromJsonAsync<Fine[]>("api/fine") ?? Array.Empty<Fine>();
    }

    /// <inheritdoc />
    public async Task<Fine[]> GetByUserId(int userId)
    {
        // Henter bøder for specifik bruger. Returnerer tomt array hvis API svarer null.
        return await _http.GetFromJsonAsync<Fine[]>($"api/fine/user/{userId}") ?? Array.Empty<Fine>();
    }

    /// <inheritdoc />
    public async Task Add(Fine fine)
    {
        // Opretter en ny bøde i backend.
        var res = await _http.PostAsJsonAsync("api/fine", ToSaveRequest(fine));
        await res.EnsureSuccessWithApiMessageAsync();
    }

    /// <inheritdoc />
    public async Task Update(Fine fine)
    {
        // Opdaterer en bøde i backend.
        var res = await _http.PutAsJsonAsync("api/fine", ToSaveRequest(fine));
        await res.EnsureSuccessWithApiMessageAsync();
    }

    /// <inheritdoc />
    public async Task Delete(int userId, int id)
    {
        // Sletter en bøde for en given bruger.
        var res = await _http.DeleteAsync($"api/fine/user/{userId}/{id}");
        await res.EnsureSuccessWithApiMessageAsync();
    }

    /// <inheritdoc />
    public Task<PagedResult<Fine>> GetPaged(
        int page, int pageSize, int? userId = null,
        string? searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null,
        decimal? minAmount = null, decimal? maxAmount = null, bool? isPaid = null)
    {
        var url = $"api/fine/paged?page={page}&pageSize={pageSize}";

        if (userId.HasValue) url += $"&userId={userId.Value}";
        if (!string.IsNullOrWhiteSpace(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (fromDate.HasValue) url += $"&fromDate={Uri.EscapeDataString(fromDate.Value.ToString("O"))}";
        if (toDate.HasValue) url += $"&toDate={Uri.EscapeDataString(toDate.Value.ToString("O"))}";
        if (minAmount.HasValue) url += $"&minAmount={minAmount.Value}";
        if (maxAmount.HasValue) url += $"&maxAmount={maxAmount.Value}";
        if (isPaid.HasValue) url += $"&isPaid={isPaid.Value.ToString().ToLowerInvariant()}";

        return _http.GetFromJsonAsync<PagedResult<Fine>>(url)!;
    }

    private static SaveFineRequest ToSaveRequest(Fine fine) => new()
    {
        Id = fine.Id,
        UserId = fine.UserId,
        Amount = fine.Amount,
        Comment = fine.Comment,
        Date = fine.Date,
        IsPaid = fine.IsPaid
    };
}
