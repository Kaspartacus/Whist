using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using WebApp;

using WebApp.Service;
using WebApp.Service.AuthServices;
using WebApp.Service.CalendarServices;
using WebApp.Service.FineServices;
using WebApp.Service.HighlightServices;
using WebApp.Service.PointServices;
using WebApp.Service.RuleServices;
using WebApp.Service.UploadServices;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ------------------------------------------------------------
// Root components (App + HeadOutlet)
// ------------------------------------------------------------
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ------------------------------------------------------------
// Authorization / browser token storage
// ------------------------------------------------------------
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<TokenStorage>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    provider => provider.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<BearerTokenHandler>();

// ------------------------------------------------------------
// HttpClient (API base address)
// ------------------------------------------------------------
builder.Services.AddScoped(provider =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
    if (string.IsNullOrWhiteSpace(apiBaseUrl))
        throw new InvalidOperationException("ApiBaseUrl is required.");

    var handler = provider.GetRequiredService<BearerTokenHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});

// ------------------------------------------------------------
// DI: Services (frontend -> kalder backend API)
// ------------------------------------------------------------
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFineService, FineService>();

builder.Services.AddScoped<IHighlightService, HighlightService>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IRuleService, RuleService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IPointService, PointService>();

await builder.Build().RunAsync();
