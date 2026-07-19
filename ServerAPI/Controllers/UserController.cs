using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServerAPI.Auth;
using ServerAPI.Configuration;
using ServerAPI.Storage;
using ServerAPI.Utils;

namespace ServerAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly CosmosDbContext _cosmos;
    private readonly IImageStorageService _imageStorage;
    private readonly ILogger<UserController> _logger;

    public UserController(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenStore refreshTokenStore,
        CosmosDbContext cosmos,
        IImageStorageService imageStorage,
        ILogger<UserController> logger)
    {
        _userManager = userManager;
        _refreshTokenStore = refreshTokenStore;
        _cosmos = cosmos;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<User[]>> GetAll(CancellationToken cancellationToken)
    {
        var users = await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users, cancellationToken);
        var includePrivateContactData = User.Identity?.IsAuthenticated == true;
        return Ok(users.Select(user => ToDto(user, includePrivateContactData)).ToArray());
    }

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<User>> GetById(int id, CancellationToken cancellationToken)
    {
        var user = await CosmosDbContext.ReadByDocumentIdAsync<ApplicationUser>(_cosmos.Users, id.ToString(), cancellationToken);
        return user is null
            ? NotFound()
            : Ok(ToDto(user, User.Identity?.IsAuthenticated == true));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] SaveUserRequest request, CancellationToken cancellationToken)
    {
        if (HasMissingRequiredProfileText(request))
            return BadRequest(new { message = "Navn, kaldenavn, email, adresse og telefonnummer skal udfyldes." });

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Adgangskode er påkrævet." });

        if (!_imageStorage.IsAllowedImageUrl(request.ImageUrl))
            return BadRequest(new { message = "Billed-URL er ikke tilladt." });

        var existingUsers = await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users, cancellationToken);
        var minimumNextUserId = existingUsers.Any() ? existingUsers.Max(user => user.Id) + 1 : 1;

        var user = new ApplicationUser
        {
            Id = await _cosmos.GetNextIdAtLeastAsync("users", minimumNextUserId, cancellationToken),
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        ApplyProfile(request, user);

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "User creation failed. Actor user: {ActorUserId}. Target email: {TargetEmail}.",
                GetCurrentUserId(),
                request.Email.Trim());
            return ValidationProblem(ToProblemDetails(result));
        }

        result = await _userManager.AddToRoleAsync(user, "Member");
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "User role assignment failed after creating user {CreatedUserId} ({CreatedEmail}). Actor user: {ActorUserId}.",
                user.Id,
                user.Email,
                GetCurrentUserId());
            return ValidationProblem(ToProblemDetails(result));
        }

        _logger.LogInformation(
            "User {CreatedUserId} ({CreatedEmail}) was created by user {ActorUserId}.",
            user.Id,
            user.Email,
            GetCurrentUserId());

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user, true));
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveUserRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(new { message = "ID i URL og body matcher ikke." });

        if (HasMissingRequiredProfileText(request))
            return BadRequest(new { message = "Navn, kaldenavn, email, adresse og telefonnummer skal udfyldes." });

        if (!_imageStorage.IsAllowedImageUrl(request.ImageUrl))
            return BadRequest(new { message = "Billed-URL er ikke tilladt." });

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        if (!CanManageUser(id))
        {
            _logger.LogWarning(
                "User update forbidden. Actor user: {ActorUserId}. Target user: {TargetUserId}.",
                GetCurrentUserId(),
                id);
            return Forbid();
        }

        var oldImageUrl = user.ImageUrl;

        ApplyProfile(request, user);
        user.UserName = request.Email.Trim();
        user.Email = request.Email.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "User update failed. Actor user: {ActorUserId}. Target user: {TargetUserId}.",
                GetCurrentUserId(),
                id);
            return ValidationProblem(ToProblemDetails(result));
        }

        _logger.LogInformation(
            "User {TargetUserId} ({TargetEmail}) was updated by user {ActorUserId}.",
            user.Id,
            user.Email,
            GetCurrentUserId());

        if (!string.IsNullOrWhiteSpace(oldImageUrl) &&
            !string.Equals(oldImageUrl, user.ImageUrl, StringComparison.Ordinal))
        {
            await _imageStorage.TryDeleteImageAsync(oldImageUrl, cancellationToken);
        }

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Admin password reset failed. Admin user: {ActorUserId}. Target user: {TargetUserId}.",
                GetCurrentUserId(),
                id);
            return ValidationProblem(ToProblemDetails(result));
        }

        result = await _userManager.UpdateSecurityStampAsync(user);
        if (result.Succeeded)
            await _refreshTokenStore.RevokeAllForUserAsync(user.Id);
        else
        {
            _logger.LogWarning(
                "Admin password reset could not update security stamp. Admin user: {ActorUserId}. Target user: {TargetUserId}.",
                GetCurrentUserId(),
                id);
        }

        if (result.Succeeded)
        {
            _logger.LogInformation(
                "Admin user {ActorUserId} reset password for user {TargetUserId} ({TargetEmail}); old tokens were revoked.",
                GetCurrentUserId(),
                user.Id,
                user.Email);
        }

        return result.Succeeded
            ? NoContent()
            : ValidationProblem(ToProblemDetails(result));
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        if (!CanManageUser(id))
        {
            _logger.LogWarning(
                "User delete forbidden. Actor user: {ActorUserId}. Target user: {TargetUserId}.",
                GetCurrentUserId(),
                id);
            return Forbid();
        }

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count <= 1)
            {
                _logger.LogWarning(
                    "Last admin delete attempt blocked. Actor user: {ActorUserId}. Target user: {TargetUserId}.",
                    GetCurrentUserId(),
                    id);
                return BadRequest(new { message = "Den sidste admin-bruger kan ikke slettes." });
            }
        }

        if (!string.IsNullOrWhiteSpace(user.ImageUrl))
            await _imageStorage.TryDeleteImageAsync(user.ImageUrl, cancellationToken);

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            await _refreshTokenStore.RevokeAllForUserAsync(user.Id);
            _logger.LogInformation(
                "User {TargetUserId} ({TargetEmail}) was deleted by user {ActorUserId}.",
                user.Id,
                user.Email,
                GetCurrentUserId());
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "User delete failed. Actor user: {ActorUserId}. Target user: {TargetUserId}.",
                GetCurrentUserId(),
                id);
            return ValidationProblem(ToProblemDetails(result));
        }

        return NoContent();
    }

    private static void ApplyProfile(SaveUserRequest request, ApplicationUser user)
    {
        user.Name = TextAutoReplace.Apply(request.Name.Trim()) ?? "";
        user.NickName = TextAutoReplace.Apply(request.NickName.Trim()) ?? "";
        user.Address = TextAutoReplace.Apply(request.Address.Trim()) ?? "";
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.BirthDate = request.BirthDate;
        user.Description = TextAutoReplace.Apply(request.Description?.Trim()) ?? "";
        user.FunFact = TextAutoReplace.Apply(request.FunFact?.Trim()) ?? "";
        user.ImageUrl = request.ImageUrl?.Trim() ?? "";
    }

    private static bool HasMissingRequiredProfileText(SaveUserRequest request)
        => string.IsNullOrWhiteSpace(request.Name) ||
           string.IsNullOrWhiteSpace(request.NickName) ||
           string.IsNullOrWhiteSpace(request.Email) ||
           string.IsNullOrWhiteSpace(request.Address) ||
           string.IsNullOrWhiteSpace(request.PhoneNumber);

    private static User ToDto(ApplicationUser user, bool includePrivateContactData) => new()
    {
        Id = user.Id,
        Name = user.Name,
        NickName = user.NickName,
        Email = includePrivateContactData ? user.Email ?? "" : "",
        Address = includePrivateContactData ? user.Address : "",
        PhoneNumber = includePrivateContactData ? user.PhoneNumber ?? "" : "",
        BirthDate = user.BirthDate,
        Description = user.Description,
        FunFact = user.FunFact,
        ImageUrl = user.ImageUrl
    };

    private static ValidationProblemDetails ToProblemDetails(IdentityResult result)
    {
        var details = new ValidationProblemDetails();
        foreach (var error in result.Errors)
            details.Errors.TryAdd(error.Code, [error.Description]);
        return details;
    }

    private bool CanManageUser(int targetUserId)
        => User.IsInRole("Admin") || GetCurrentUserId() == targetUserId;

    private int? GetCurrentUserId()
    {
        var value = User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }
}
