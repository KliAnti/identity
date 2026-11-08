using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Identity.Infrastructure.Persistence.Models.User;

namespace Identity.Infrastructure;

using Identity.Infrastructure.Persistence.Models.User;

/// <summary>
/// Вычисляет и проверяет хеши паролей с солью.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher<UserModel>
{
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MaximumAcceptedIterations = 2_000_000;

    /// <summary>
    /// Вычисляет хеш пароля и сохраняет сгенерированную соль в сущности пользователя.
    /// </summary>
    public string HashPassword(UserModel user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        user.PasswordSalt = salt;
        return $"v1$pbkdf2-sha256${Iterations.ToString(CultureInfo.InvariantCulture)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Проверяет пароль по сохранённым хешу и соли.
    /// </summary>
    public PasswordVerificationResult VerifyHashedPassword(
        UserModel user,
        string hashedPassword,
        string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(hashedPassword);
        ArgumentNullException.ThrowIfNull(providedPassword);

        if (user.PasswordSalt is not { Length: SaltSize })
        {
            return PasswordVerificationResult.Failed;
        }

        var parts = hashedPassword.Split('$');
        if (parts.Length != 4 ||
            parts[0] != "v1" ||
            parts[1] != "pbkdf2-sha256" ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
            iterations < 100_000 ||
            iterations > MaximumAcceptedIterations)
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        if (expectedHash.Length != HashSize)
        {
            return PasswordVerificationResult.Failed;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            providedPassword,
            user.PasswordSalt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
        {
            return PasswordVerificationResult.Failed;
        }

        return iterations < Iterations
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }
}
