using Microsoft.EntityFrameworkCore;
using Identity.Infrastructure.Persistence.Models.Device;
using Identity.Infrastructure.Persistence.Models.Role;
using Identity.Infrastructure.Persistence.Models.Session;
using Identity.Infrastructure.Persistence.Models.User;
using Identity.Infrastructure.Persistence.Models.UserRole;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Контекст пользовательской схемы и стандартных сущностей OpenIddict.
/// </summary>
/// <param name="options">Параметры подключения и конфигурации EF Core.</param>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// Получает набор пользовательских учётных записей.
    /// </summary>
    public DbSet<UserModel> Users => Set<UserModel>();

    /// <summary>
    /// Получает набор глобальных ролей.
    /// </summary>
    public DbSet<RoleModel> Roles => Set<RoleModel>();

    /// <summary>
    /// Получает набор назначений ролей пользователям.
    /// </summary>
    public DbSet<UserRoleModel> UserRoles => Set<UserRoleModel>();

    /// <summary>
    /// Получает набор зарегистрированных устройств.
    /// </summary>
    public DbSet<DeviceModel> Devices => Set<DeviceModel>();

    /// <summary>
    /// Получает набор пользовательских сессий.
    /// </summary>
    public DbSet<SessionModel> Sessions => Set<SessionModel>();

    /// <summary>
    /// Настраивает таблицы, ключи, связи и индексы модели данных.
    /// </summary>
    /// <param name="builder">Построитель модели EF Core.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseOpenIddict<Guid>();
        builder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
