namespace Identity.Infrastructure.Persistence.Models.Session;

/// <summary>
/// Пользовательская сессия на конкретном устройстве.
/// </summary>
public sealed class SessionModel
{
    /// <summary>
    /// Получает или задаёт идентификатор сессии.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Получает или задаёт идентификатор устройства сессии.
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// Получает или задаёт время создания сессии.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Получает или задаёт время истечения сессии.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Получает или задаёт время отзыва сессии.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
