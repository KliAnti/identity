namespace Identity.Infrastructure.Persistence.Models.Role;

/// <summary>
/// Глобальная роль пользователя.
/// </summary>
public sealed class RoleModel
{
    /// <summary>
    /// Получает или задаёт идентификатор роли.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Получает или задаёт имя роли.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Получает или задаёт нормализованное имя для поиска.
    /// </summary>
    public required string NormalizedName { get; set; }
}
