using System.Net;
using System.Net.Http.Json;
using Core;
using Microsoft.AspNetCore.Components.Authorization;
using WebApp.Service.ApiErrors;

namespace WebApp.Service.AuthServices;

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly TokenStorage _tokenStorage;
    private readonly JwtAuthenticationStateProvider _authenticationStateProvider;

    public AuthService(
        HttpClient http,
        TokenStorage tokenStorage,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _http = http;
        _tokenStorage = tokenStorage;
        _authenticationStateProvider = (JwtAuthenticationStateProvider)authenticationStateProvider;
    }

    public async Task<bool> Login(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return false;

        var login = await response.ReadFromJsonOrThrowAsync<LoginResponse>();
        if (login is null ||
            string.IsNullOrWhiteSpace(login.AccessToken) ||
            string.IsNullOrWhiteSpace(login.RefreshToken))
        {
            return false;
        }

        await _tokenStorage.SetTokensAsync(login.AccessToken, login.RefreshToken);
        _authenticationStateProvider.NotifyAuthenticationChanged();
        return true;
    }

    public async Task Logout()
    {
        try
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            await _http.PostAsJsonAsync("api/auth/logout", new LogoutRequest
            {
                RefreshToken = refreshToken
            });
        }
        finally
        {
            await _tokenStorage.RemoveTokensAsync();
            _authenticationStateProvider.NotifyAuthenticationChanged();
        }
    }

    public async Task<User?> GetCurrentUser()
    {
        var authenticatedUser = await _authenticationStateProvider.GetAuthenticatedUserAsync();
        return authenticatedUser is null
            ? null
            : new User
            {
                Id = authenticatedUser.Id,
                Name = authenticatedUser.Name,
                Email = authenticatedUser.Email
            };
    }

    public async Task ChangePassword(string currentPassword, string newPassword)
    {
        var response = await _http.PostAsJsonAsync("api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        });

        var login = await response.ReadFromJsonOrThrowAsync<LoginResponse>();
        if (login is null ||
            string.IsNullOrWhiteSpace(login.AccessToken) ||
            string.IsNullOrWhiteSpace(login.RefreshToken))
        {
            return;
        }

        await _tokenStorage.SetTokensAsync(login.AccessToken, login.RefreshToken);
        _authenticationStateProvider.NotifyAuthenticationChanged();
    }
}
