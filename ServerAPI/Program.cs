using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using ServerAPI.Auth;
using ServerAPI.Configuration;
using ServerAPI.Repositories.Calendars;
using ServerAPI.Repositories.Fines;
using ServerAPI.Repositories.Highlights;
using ServerAPI.Repositories.Points;
using ServerAPI.Repositories.Rules;
using ServerAPI.Services.Reminders;
using ServerAPI.Storage;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// Services (Dependency Injection)
// =========================================================

// Controllers (API endpoints)
builder.Services.AddControllers();

// Swagger (API dokumentation / test)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Persistence
builder.Services.AddSingleton<CosmosDbContext>();
builder.Services.AddSingleton<IFineRepository, FineRepositoryCosmosDb>();
builder.Services.AddSingleton<IHighlightRepository, HighlightRepositoryCosmosDb>();
builder.Services.AddSingleton<IRuleRepository, RuleRepositoryCosmosDb>();
builder.Services.AddSingleton<ICalendarRepository, CalendarRepositoryCosmosDb>();
builder.Services.AddSingleton<IPointRepository, PointRepositoryCosmosDb>();

builder.Services.AddScoped<IUserStore<ApplicationUser>, CosmosUserStore>();
builder.Services.AddScoped<IRoleStore<ApplicationRole>, CosmosRoleStore>();
builder.Services.AddScoped<IdentityDataInitializer>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenStore, CosmosRefreshTokenStore>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<IReminderMailService, ReminderMailService>();

builder.Services
    .AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetSection(BlobStorageOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "BlobStorage:ConnectionString is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ContainerName), "BlobStorage:ContainerName is required.")
    .Validate(options => options.MaxUploadBytes is > 0 and <= 5 * 1024 * 1024, "BlobStorage:MaxUploadBytes must be between 1 byte and 5 MB.")
    .Validate(options => options.MaxImageWidth is >= 100 and <= 4000, "BlobStorage:MaxImageWidth must be between 100 and 4000.")
    .Validate(options => options.WebpQuality is >= 50 and <= 95, "BlobStorage:WebpQuality must be between 50 and 95.")
    .ValidateOnStart();

builder.Services.AddSingleton<IImageStorageService, BlobImageStorageService>();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<ApplicationRole>()
    .AddDefaultTokenProviders();

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
    .Validate(options => options.Key.Length >= 32, "Jwt:Key must be at least 32 characters.")
    .Validate(options => options.AccessTokenMinutes is >= 5 and <= 120, "Jwt:AccessTokenMinutes must be between 5 and 120.")
    .Validate(options => options.RefreshTokenDays is >= 1 and <= 30, "Jwt:RefreshTokenDays must be between 1 and 30.")
    .ValidateOnStart();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtValidation");
                var userId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                var tokenSecurityStamp = context.Principal?.FindFirst(AuthClaimTypes.SecurityStamp)?.Value;
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tokenSecurityStamp))
                {
                    logger.LogWarning(
                        "JWT rejected for {Path}: missing required claims. IP: {IpAddress}.",
                        context.HttpContext.Request.Path,
                        context.HttpContext.Connection.RemoteIpAddress);
                    context.Fail("Token is missing required authentication claims.");
                    return;
                }

                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByIdAsync(userId);
                if (user is null)
                {
                    logger.LogWarning(
                        "JWT rejected for {Path}: user {UserId} no longer exists. IP: {IpAddress}.",
                        context.HttpContext.Request.Path,
                        userId,
                        context.HttpContext.Connection.RemoteIpAddress);
                    context.Fail("User no longer exists.");
                    return;
                }

                var currentSecurityStamp = await userManager.GetSecurityStampAsync(user);
                if (!string.Equals(currentSecurityStamp, tokenSecurityStamp, StringComparison.Ordinal))
                {
                    logger.LogWarning(
                        "JWT rejected for user {UserId} ({Email}): token was revoked or password/logout changed security stamp.",
                        user.Id,
                        user.Email);
                    context.Fail("Token has been revoked.");
                }
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtValidation");
                logger.LogWarning(
                    "JWT authentication failed for {Path}. IP: {IpAddress}. Reason: {ErrorMessage}",
                    context.HttpContext.Request.Path,
                    context.HttpContext.Connection.RemoteIpAddress,
                    context.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting");
        logger.LogWarning(
            "Rate limit hit on {Path}. User: {UserId}. IP: {IpAddress}.",
            context.HttpContext.Request.Path,
            GetAuthenticatedUserId(context.HttpContext) ?? "anonymous",
            context.HttpContext.Connection.RemoteIpAddress);

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "For mange forsøg. Prøv igen om lidt." },
            cancellationToken);
    };

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientIp(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("upload", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetAuthenticatedUserId(httpContext) ?? GetClientIp(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("reminders", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"reminders:{GetClientIp(httpContext)}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Reminder mails are triggered by GitHub Actions through api/reminder/run.
// Keeping scheduling outside the API lets the Container App scale to zero safely.

// CORS: API only accepts configured frontend origins.
// Production should set Cors:AllowedOrigins to the real frontend domain.
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Length == 0)
        throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one allowed origin.");

    options.AddPolicy("FrontendOnly", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// =========================================================
// Middleware pipeline (rækkefølgen betyder noget)
// =========================================================

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");

        logger.LogError(
            exceptionFeature?.Error,
            "Unhandled exception while processing {Method} {Path}. TraceId: {TraceId}",
            context.Request.Method,
            exceptionFeature?.Path ?? context.Request.Path.Value,
            context.TraceIdentifier);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Der skete en serverfejl. Prøv igen om lidt.",
            traceId = context.TraceIdentifier
        });
    });
});

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");

    await next();
});

// Swagger kun i development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Static files. Legacy uploaded files under wwwroot/uploads can still be served,
// while new uploads are stored in Azure Blob Storage.
app.UseStaticFiles();

// Redirect HTTP -> HTTPS
app.UseHttpsRedirection();

// CORS (skal ligge før MapControllers så den gælder for API endpoints)
app.UseCors("FrontendOnly");

// Auth middleware
app.UseAuthentication();

// Rate limiting for selected endpoints (login/upload)
app.UseRateLimiter();

app.UseAuthorization();


// Map controllers (api/*)
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapControllers();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("StartupInitialization");

    _ = Task.Run(async () =>
    {
        try
        {
            logger.LogInformation("Starting background identity and database initialization.");

            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider
                .GetRequiredService<IdentityDataInitializer>()
                .InitializeAsync(app.Lifetime.ApplicationStopping);

            logger.LogInformation("Background identity and database initialization completed.");
        }
        catch (OperationCanceledException) when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            logger.LogInformation("Background identity and database initialization was cancelled because the app is stopping.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Background identity and database initialization failed.");
        }
    }, app.Lifetime.ApplicationStopping);
});

app.Run();

static string GetClientIp(HttpContext context)
    => $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

static string? GetAuthenticatedUserId(HttpContext context)
{
    var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    return string.IsNullOrWhiteSpace(userId) ? null : $"user:{userId}";
}
