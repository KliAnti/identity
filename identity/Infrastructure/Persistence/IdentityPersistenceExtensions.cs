using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Identity.Infrastructure.Persistence.Stores;
using Identity.Infrastructure.Persistence.Models.Role;
using Identity.Infrastructure.Persistence.Models.User;
using Identity.Infrastructure;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Методы регистрации компонентов постоянного хранилища Identity.
/// </summary>
public static class IdentityPersistenceExtensions
{
    /// <summary>
    /// Регистрирует PostgreSQL-контекст и хранилища Identity для пользовательской схемы.
    /// </summary>
    /// <param name="services">Коллекция сервисов приложения.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Коллекция сервисов для последовательной настройки.</returns>
    /// <exception cref="InvalidOperationException">
    /// Возникает, если строка подключения «IdentityDb» не задана.
    /// </exception>
    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException(
            "Не задана строка подключения 'ConnectionStrings:IdentityDb'.");

        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<UserModel>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<RoleModel>()
            .AddUserStore<IdentityUserStore>()
            .AddRoleStore<IdentityRoleStore>();
        services.AddScoped<IPasswordHasher<UserModel>, Pbkdf2PasswordHasher>();

        return services;
    }
}
