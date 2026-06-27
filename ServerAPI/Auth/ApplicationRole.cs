using Microsoft.AspNetCore.Identity;
using MongoDB.Bson.Serialization.Attributes;

namespace ServerAPI.Auth;

[BsonIgnoreExtraElements]
public sealed class ApplicationRole : IdentityRole<int>;
