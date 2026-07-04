using Microsoft.AspNetCore.Identity;
using Core;

namespace ServerAPI.Auth;

public sealed class ApplicationUser : IdentityUser<int>
{
    public string Name { get; set; } = "";
    public string NickName { get; set; } = "";
    public string Address { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public string Description { get; set; } = "";
    public string FunFact { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public int? LastBirthdayGreetingSentYear { get; set; }
    public List<string> Roles { get; set; } = new();
    // Legacy embedded fines used only for one-time migration to the fines container.
    public ICollection<Fine> Fines { get; set; } = new List<Fine>();
}
