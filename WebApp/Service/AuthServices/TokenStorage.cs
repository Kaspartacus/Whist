using Microsoft.JSInterop;

namespace WebApp.Service.AuthServices;

public sealed class TokenStorage
{
    private const string AccessTokenKey = "whist.accessToken";
    private const string RefreshTokenKey = "whist.refreshToken";
    private readonly IJSRuntime _jsRuntime;

    public TokenStorage(IJSRuntime jsRuntime) => _jsRuntime = jsRuntime;

    public ValueTask<string?> GetAccessTokenAsync()
        => _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", AccessTokenKey);

    public ValueTask<string?> GetRefreshTokenAsync()
        => _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", RefreshTokenKey);

    public async ValueTask SetTokensAsync(string accessToken, string refreshToken)
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", AccessTokenKey, accessToken);
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", RefreshTokenKey, refreshToken);
    }

    public async ValueTask RemoveTokensAsync()
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", AccessTokenKey);
        await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", RefreshTokenKey);
    }

    public ValueTask RemoveLegacyAuthenticationDataAsync()
        => _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "currentUser");
}
