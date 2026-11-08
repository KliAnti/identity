namespace Identity.Infrastructure.Persistence.Models.User;

/// <summary>
/// Пользовательская учётная запись.
/// </summary>
public sealed class UserModel
{
    /// <summary>
    /// Получает или задаёт идентификатор пользователя.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Получает или задаёт адрес электронной почты.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Получает или задаёт нормализованный адрес электронной почты для поиска.
    /// </summary>
    public required string NormalizedEmail { get; set; }

    /// <summary>
    /// Получает или задаёт признак подтверждения адреса электронной почты.
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Получает или задаёт хеш пароля.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Получает или задаёт соль пароля.
    /// </summary>
    public byte[]? PasswordSalt { get; set; }
}
