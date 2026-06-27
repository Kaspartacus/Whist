using Microsoft.AspNetCore.Identity;
using MongoDB.Bson.Serialization.Attributes;
using Core;

namespace ServerAPI.Auth;

[BsonIgnoreExtraElements]
public sealed class ApplicationUser : IdentityUser<int>
{
    public string Name { get; set; } = "";
    public string NickName { get; set; } = "";
    public string Address { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public string Description { get; set; } = "";
    public string FunFact { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public ICollection<Fine> Fines { get; set; } = new List<Fine>();
}
