using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServerAPI.Auth;
using ServerAPI.Configuration;

namespace ServerAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly CosmosDbContext _cosmos;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UserController> _logger;

    public UserController(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenStore refreshTokenStore,
        CosmosDbContext cosmos,
        IWebHostEnvironment environment,
        ILogger<UserController> logger)
    {
        _userManager = userManager;
        _refreshTokenStore = refreshTokenStore;
        _cosmos = cosmos;
        _environment = environment;
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

        var existingUsers = await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users, cancellationToken);
        var lastUser = existingUsers.MaxBy(user => user.Id);

        var user = new ApplicationUser
        {
            Id = (lastUser?.Id ?? 0) + 1,
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
            return ValidationProblem(ToProblemDetails(result));

        _logger.LogInformation(
            "User {CreatedUserId} ({CreatedEmail}) was created by user {ActorUserId}.",
            user.Id,
            user.Email,
            GetCurrentUserId());

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user, true));
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveUserRequest request)
    {
        if (id != request.Id)
            return BadRequest(new { message = "ID i URL og body matcher ikke." });

        if (HasMissingRequiredProfileText(request))
            return BadRequest(new { message = "Navn, kaldenavn, email, adresse og telefonnummer skal udfyldes." });

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        if (!CanManageUser(id))
            return Forbid();

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
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        if (!CanManageUser(id))
            return Forbid();

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count <= 1)
                return BadRequest(new { message = "Den sidste admin-bruger kan ikke slettes." });
        }

        if (!string.IsNullOrWhiteSpace(user.ImageUrl))
            TryDeleteProfileImage(user.ImageUrl);

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

        return result.Succeeded
            ? NoContent()
            : ValidationProblem(ToProblemDetails(result));
    }

    private static void ApplyProfile(SaveUserRequest request, ApplicationUser user)
    {
        user.Name = request.Name.Trim();
        user.NickName = request.NickName.Trim();
        user.Address = request.Address.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.BirthDate = request.BirthDate;
        user.Description = request.Description?.Trim() ?? "";
        user.FunFact = request.FunFact?.Trim() ?? "";
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

    private void TryDeleteProfileImage(string imageUrl)
    {
        try
        {
            var relativePath = imageUrl.Replace($"{Request.Scheme}://{Request.Host}", "");
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            // Profile deletion should not fail solely because an old image cannot be removed.
            _logger.LogWarning(ex, "Could not delete old profile image for URL {ImageUrl}.", imageUrl);
        }
    }

    private bool CanManageUser(int targetUserId)
        => User.IsInRole("Admin") || GetCurrentUserId() == targetUserId;

    private int? GetCurrentUserId()
    {
        var value = User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }
}
