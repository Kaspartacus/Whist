using Core;

namespace WebApp.Service;

/// <summary>
/// Kontrakt for frontend service til brugere.
/// UI-laget bruger denne til at hente/rette/slette medlemmer.
/// </summary>
public interface IUserService
{
    /// <summary>Henter alle brugere.</summary>
    Task<User[]> GetAll();

    /// <summary>Henter én bruger ud fra id.</summary>
    Task<User?> GetById(int id);

    /// <summary>Opretter en ny bruger.</summary>
    Task AddUser(User user, string password);

    /// <summary>Sletter en bruger ud fra id.</summary>
    Task Delete(int id);

    /// <summary>Opdaterer en eksisterende bruger.</summary>
    Task Update(User user);

    /// <summary>Nulstiller en brugers kodeord som admin.</summary>
    Task ResetPassword(int id, string newPassword);
}
