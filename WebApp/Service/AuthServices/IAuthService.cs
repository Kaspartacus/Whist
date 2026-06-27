using Core;

namespace WebApp.Service.AuthServices;

public interface IAuthService
{
    Task<bool> Login(string email, string password);
    Task Logout();
    Task<User?> GetCurrentUser();
    Task ChangePassword(string currentPassword, string newPassword);
}
