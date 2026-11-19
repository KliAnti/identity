namespace Identity.Infrastructure.Persistence.Models.UserRole;

using Identity.Infrastructure.Persistence.Models.Role;
using Identity.Infrastructure.Persistence.Models.User;

/// <summary>
/// Связь пользователя с назначенной ему ролью.
/// </summary>
public sealed class UserRoleModel
{
    /// <summary>
    /// Получает или задаёт идентификатор пользователя.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Получает или задаёт идентификатор роли.
    /// </summary>
    public Guid RoleId { get; set; }
}
