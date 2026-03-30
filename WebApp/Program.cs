using Blazored.LocalStorage;
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
// 3rd party / browser storage
// ------------------------------------------------------------
builder.Services.AddBlazoredLocalStorage();

// ------------------------------------------------------------
// HttpClient (API base address)
// OBS: Dette er hardcoded til localhost (dev).
// ------------------------------------------------------------
builder.Services.AddScoped(_ =>
    new HttpClient
    {
        BaseAddress = new Uri("http://localhost:5176/")
    });

// ------------------------------------------------------------
// DI: Services (frontend -> kalder backend API)
// ------------------------------------------------------------
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// NOTE: Fine bruger mock lige nu (som du også har haft i flowet).
builder.Services.AddScoped<IFineService, FineService>();

builder.Services.AddScoped<IHighlightService, HighlightService>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IRuleService, RuleService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IPointService, PointService>();

await builder.Build().RunAsync();
