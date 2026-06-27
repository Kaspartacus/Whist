using System.Net.Http.Json;
using Core;
using WebApp.Service.ApiErrors;

namespace WebApp.Service;

/// <summary>
/// HTTP-baseret UserService.
/// 
/// Bemærk:
/// - Endpoints matcher UserController i backend.
/// - Vi ændrer ikke funktionaliteten (samme routes og payloads).
/// </summary>
public class UserService : IUserService
{
    private readonly HttpClient _http;
    private const string BaseRoute = "api/user";

    public UserService(HttpClient http)
    {
        _http = http;
    }

    public async Task<User[]> GetAll() => await _http.GetFromJsonAsync<User[]>(BaseRoute) ?? Array.Empty<User>();
    

    public async Task<User?> GetById(int id) => await _http.GetFromJsonAsync<User?>($"{BaseRoute}/{id}");
    

    public async Task AddUser(User user, string password)
    {
        var request = ToSaveRequest(user, password);
        var res = await _http.PostAsJsonAsync(BaseRoute, request);
        await res.EnsureSuccessWithApiMessageAsync();
    }

    public async Task Delete(int id)
    {
        var res = await _http.DeleteAsync($"{BaseRoute}/{id}");
        await res.EnsureSuccessWithApiMessageAsync();
    }

    public async Task Update(User user)
    {
        var request = ToSaveRequest(user, password: null);
        var res = await _http.PutAsJsonAsync($"{BaseRoute}/{user.Id}", request);
        await res.EnsureSuccessWithApiMessageAsync();
    }

    public async Task ResetPassword(int id, string newPassword)
    {
        var res = await _http.PostAsJsonAsync($"{BaseRoute}/{id}/reset-password", new ResetPasswordRequest
        {
            NewPassword = newPassword
        });
        await res.EnsureSuccessWithApiMessageAsync();
    }

    private static SaveUserRequest ToSaveRequest(User user, string? password)
    {
        return new SaveUserRequest
        {
            Id = user.Id,
            Name = user.Name,
            NickName = user.NickName,
            Email = user.Email,
            Password = password,
            Address = user.Address,
            PhoneNumber = user.PhoneNumber,
            BirthDate = user.BirthDate,
            Description = user.Description,
            FunFact = user.FunFact,
            ImageUrl = user.ImageUrl
        };
    }
}
