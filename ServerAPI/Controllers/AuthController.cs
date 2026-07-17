using System.IdentityModel.Tokens.Jwt;
using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerAPI.Auth;

namespace ServerAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IRefreshTokenStore refreshTokenStore,
        RefreshTokenService refreshTokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _refreshTokenStore = refreshTokenStore;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            _logger.LogWarning(
                "Login failed for unknown email {Email}. IP: {IpAddress}.",
                request.Email.Trim(),
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Forkert email eller adgangskode." });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning(
                "Login blocked because user {UserId} ({Email}) is locked out. IP: {IpAddress}.",
                user.Id,
                user.Email,
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Forkert email eller adgangskode." });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            _logger.LogWarning(
                "Login failed for user {UserId} ({Email}). IP: {IpAddress}.",
                user.Id,
                user.Email,
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Forkert email eller adgangskode." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        _logger.LogInformation(
            "Login succeeded for user {UserId} ({Email}). Roles: {Roles}.",
            user.Id,
            user.Email,
            string.Join(", ", roles));
        return Ok(await CreateLoginResponse(user, roles.ToArray()));
    }

    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshTokenService.Hash(request.RefreshToken);
        var existingToken = await _refreshTokenStore.FindByHashAsync(tokenHash, cancellationToken);
        if (existingToken is null)
        {
            _logger.LogWarning("Refresh token rejected because it was not found. IP: {IpAddress}.", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Sessionen er udløbet. Log ind igen." });
        }

        if (!existingToken.IsActive)
        {
            _logger.LogWarning(
                "Inactive refresh token was used for user {UserId}. All refresh tokens for that user were revoked.",
                existingToken.UserId);
            await _refreshTokenStore.RevokeAllForUserAsync(existingToken.UserId, cancellationToken);
            return Unauthorized(new { message = "Sessionen er udløbet. Log ind igen." });
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());
        if (user is null)
        {
            await _refreshTokenStore.RevokeAsync(tokenHash, cancellationToken: cancellationToken);
            _logger.LogWarning("Refresh token rejected because user {UserId} no longer exists.", existingToken.UserId);
            return Unauthorized(new { message = "Sessionen er udløbet. Log ind igen." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var login = await CreateLoginResponse(user, roles.ToArray(), cancellationToken);
        var newTokenHash = RefreshTokenService.Hash(login.RefreshToken);
        await _refreshTokenStore.RevokeAsync(tokenHash, newTokenHash, cancellationToken);

        _logger.LogInformation(
            "Refresh token rotated for user {UserId} ({Email}).",
            user.Id,
            user.Email);

        return Ok(login);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            await _refreshTokenStore.RevokeAsync(RefreshTokenService.Hash(request.RefreshToken), cancellationToken: cancellationToken);

        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return NoContent();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NoContent();

        var result = await _userManager.UpdateSecurityStampAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation(
                "User {UserId} ({Email}) logged out and current tokens were revoked.",
                user.Id,
                user.Email);
        }
        else
        {
            _logger.LogWarning(
                "Logout could not update security stamp for user {UserId} ({Email}).",
                user.Id,
                user.Email);
        }

        return result.Succeeded
            ? NoContent()
            : ValidationProblem(ToProblemDetails(result));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<LoginResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Password change failed for user {UserId} ({Email}).",
                user.Id,
                user.Email);
            return ValidationProblem(ToProblemDetails(result));
        }

        result = await _userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Password change could not update security stamp for user {UserId} ({Email}).",
                user.Id,
                user.Email);
            return ValidationProblem(ToProblemDetails(result));
        }

        await _refreshTokenStore.RevokeAllForUserAsync(user.Id);
        var roles = await _userManager.GetRolesAsync(user);
        _logger.LogInformation(
            "Password changed for user {UserId} ({Email}); old tokens were revoked.",
            user.Id,
            user.Email);
        return Ok(await CreateLoginResponse(user, roles.ToArray()));
    }

    private async Task<LoginResponse> CreateLoginResponse(
        ApplicationUser user,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var login = _jwtTokenService.CreateToken(user, roles);
        var refreshToken = await _refreshTokenService.CreateAsync(user.Id, cancellationToken);
        login.RefreshToken = refreshToken.RawToken;
        login.RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc;
        return login;
    }

    private static ValidationProblemDetails ToProblemDetails(IdentityResult result)
    {
        var details = new ValidationProblemDetails();
        foreach (var error in result.Errors)
            details.Errors.TryAdd(error.Code, [error.Description]);
        return details;
    }
}
