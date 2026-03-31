using ServerAPI.Repositories;
using ServerAPI.Repositories.Calendars;
using ServerAPI.Repositories.Fines;
using ServerAPI.Repositories.Highlights;
using ServerAPI.Repositories.Points;
using ServerAPI.Repositories.Rules;
using ServerAPI.Workers;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// Services (Dependency Injection)
// =========================================================

// Controllers (API endpoints)
builder.Services.AddControllers();

// Swagger (API dokumentation / test)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Repositories (MongoDB implementeringer)
// Singleton er ok her, så længe dine repos bruger thread-safe MongoClient (typisk).
builder.Services.AddSingleton<IUserRepository, UserRepositoryMongoDB>();
builder.Services.AddSingleton<IFineRepository, FineRepositoryMongoDB>();
builder.Services.AddSingleton<IHighlightRepository, HighlightRepositoryMongoDB>();
builder.Services.AddSingleton<IRuleRepository, RuleRepositoryMongoDB>();
builder.Services.AddSingleton<ICalendarRepository, CalendarRepositoryMongoDB>();
builder.Services.AddSingleton<IPointRepository, PointRepositoryMongoDB>();

builder.Services
    .AddAuthentication("DevKey")
    .AddScheme<AuthenticationSchemeOptions, DevKeyAuthHandler>("DevKey", options => { });



// Authorization er slået til
builder.Services.AddAuthorization();

// Background worker (mail reminders)
builder.Services.AddHostedService<MailReminderWorker>();

// CORS (frontend tilladt origin i dev)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5102")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// =========================================================
// Middleware pipeline (rækkefølgen betyder noget)
// =========================================================

// Swagger kun i development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Static files (uploads under wwwroot/uploads)
app.UseStaticFiles();

// Redirect HTTP -> HTTPS
app.UseHttpsRedirection();

// CORS (skal ligge før MapControllers så den gælder for API endpoints)
app.UseCors();

// Auth middleware
// OBS: UseAuthentication gør i praksis ingenting før du har builder.Services.AddAuthentication(...)
// Men det er fint at lade den stå, hvis du planlægger auth senere.
app.UseAuthentication();
app.UseAuthorization();


// Map controllers (api/*)
app.MapControllers();

app.Run();
