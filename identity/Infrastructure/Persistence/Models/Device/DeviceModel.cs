namespace Identity.Infrastructure.Persistence.Models.Device;

/// <summary>
/// Устройство, с которого пользователь входит в систему.
/// </summary>
public sealed class DeviceModel
{
    /// <summary>
    /// Получает или задаёт идентификатор устройства.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Получает или задаёт идентификатор владельца устройства.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Получает или задаёт время регистрации устройства.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Получает или задаёт отображаемое имя устройства.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Получает или задаёт платформу устройства.
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// Получает или задаёт время последней активности устройства.
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>
    /// Получает или задаёт время отзыва устройства.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
