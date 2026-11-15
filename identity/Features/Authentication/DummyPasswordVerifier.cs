using System.Security.Cryptography;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence.Models.User;
using Microsoft.AspNetCore.Identity;

namespace Identity.Features.Authentication;

public sealed class DummyPasswordVerifier
{
    private static readonly Lazy<Credential> DummyCredential = new(CreateCredential);
    private readonly Pbkdf2PasswordHasher _passwordHasher = new();

    public DummyPasswordVerifier()
    {
        _ = DummyCredential.Value;
    }

    public void Verify(string password)
    {
        var credential = DummyCredential.Value;
        _ = _passwordHasher.VerifyHashedPassword(credential.User, credential.PasswordHash, password);
    }

    private static Credential CreateCredential()
    {
        var user = new UserModel
        {
            Id = Guid.Empty,
            Email = "dummy@example.invalid",
            NormalizedEmail = "DUMMY@EXAMPLE.INVALID"
        };
        var randomPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var passwordHash = new Pbkdf2PasswordHasher().HashPassword(user, randomPassword);
        return new Credential(user, passwordHash);
    }

    private sealed record Credential(UserModel User, string PasswordHash);
}
