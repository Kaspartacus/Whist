using Core;

namespace ServerAPI.Auth;

public interface IJwtTokenService
{
    LoginResponse CreateToken(ApplicationUser user, IReadOnlyCollection<string> roles);
}
