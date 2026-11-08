using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Методы регистрации компонентов постоянного хранилища Identity.
/// </summary>
public static class IdentityPersistenceExtensions
{
    /// <summary>
    /// Регистрирует PostgreSQL-контекст и хранилища ASP.NET Core Identity.
    /// </summary>
    /// <param name="services">Коллекция сервисов приложения.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Коллекция сервисов для последовательной настройки.</returns>
    /// <exception cref="InvalidOperationException">
    /// Возникает, если строка подключения «Identity» не задана.
    /// </exception>
    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Identity")
            ?? throw new InvalidOperationException(
                "Не задана строка подключения 'ConnectionStrings:Identity'.");

        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<IdentityUserEntity>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<IdentityDbContext>();

        return services;
    }
}
