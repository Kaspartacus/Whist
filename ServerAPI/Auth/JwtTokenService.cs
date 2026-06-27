using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ServerAPI.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IOptions<JwtOptions> options, ILogger<JwtTokenService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public LoginResponse CreateToken(ApplicationUser user, IReadOnlyCollection<string> roles)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("name", user.Name),
            new("email", user.Email ?? ""),
            new(AuthClaimTypes.SecurityStamp, user.SecurityStamp ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(DateTime.UtcNow).ToString(),
                ClaimValueTypes.Integer64)
        };

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        _logger.LogInformation(
            "JWT issued for user {UserId} ({Email}). Roles: {Roles}. Expires at {ExpiresAtUtc:u}.",
            user.Id,
            user.Email,
            string.Join(", ", roles),
            expiresAtUtc);

        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAtUtc,
            User = new AuthenticatedUser
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? "",
                Roles = roles.ToArray()
            }
        };
    }
}
