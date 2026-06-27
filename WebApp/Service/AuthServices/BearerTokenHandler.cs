using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Core;
using Microsoft.AspNetCore.Components.Authorization;

namespace WebApp.Service.AuthServices;

public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly TokenStorage _tokenStorage;
    private readonly JwtAuthenticationStateProvider _authenticationStateProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public BearerTokenHandler(TokenStorage tokenStorage, AuthenticationStateProvider authenticationStateProvider)
    {
        _tokenStorage = tokenStorage;
        _authenticationStateProvider = (JwtAuthenticationStateProvider)authenticationStateProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var isAuthRequest = IsAuthRequest(request);
        var retryRequest = !isAuthRequest
            ? await CloneRequestAsync(request, cancellationToken)
            : null;

        var accessToken = await _tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthRequest)
        {
            if (await TryRefreshAsync(request.RequestUri, cancellationToken))
            {
                response.Dispose();
                var newAccessToken = await _tokenStorage.GetAccessTokenAsync();
                if (!string.IsNullOrWhiteSpace(newAccessToken) && retryRequest is not null)
                {
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
                    return await base.SendAsync(retryRequest, cancellationToken);
                }
            }

            await _tokenStorage.RemoveTokensAsync();
            _authenticationStateProvider.NotifyAuthenticationChanged();
        }

        retryRequest?.Dispose();
        return response;
    }

    private async Task<bool> TryRefreshAsync(Uri? requestUri, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken) || requestUri is null)
                return false;

            var refreshUri = new Uri(requestUri, "/api/auth/refresh");
            using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, refreshUri)
            {
                Content = JsonContent.Create(new RefreshTokenRequest
                {
                    RefreshToken = refreshToken
                })
            };

            using var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken);
            if (!refreshResponse.IsSuccessStatusCode)
                return false;

            var login = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
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
        finally
        {
            _refreshLock.Release();
        }
    }

    private static bool IsAuthRequest(HttpRequestMessage request)
        => request.RequestUri?.AbsolutePath.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
            clone.Options.TryAdd(option.Key, option.Value);

        return clone;
    }
}
