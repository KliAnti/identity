using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

namespace Identity.Tests.Tests.Helpers;

/// <summary>
/// Создаёт контейнер сервисов Identity с конфигурацией для тестов постоянного хранилища.
/// </summary>
internal static class IdentityPersistenceTestServices
{
    /// <summary>
    /// Настраивает зависимости Identity и EF Core для работы с указанной базой PostgreSQL.
    /// </summary>
    /// <param name="connectionString">Строка подключения к тестовой базе PostgreSQL.</param>
    /// <returns>Собранный контейнер зависимостей, из которого тесты получают нужные компоненты.</returns>
    public static ServiceProvider Create(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Identity"] = connectionString
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddIdentityPersistence(configuration);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Создаёт сервисы Identity, подключённые к заданному соединению SQLite.
    /// </summary>
    /// <param name="connection">Открытое соединение с тестовой базой SQLite.</param>
    /// <returns>Построенный контейнер сервисов.</returns>
    public static ServiceProvider CreateForSqlite(SqliteConnection connection)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<IdentityUserEntity>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<IdentityDbContext>();

        return services.BuildServiceProvider();
    }
}
