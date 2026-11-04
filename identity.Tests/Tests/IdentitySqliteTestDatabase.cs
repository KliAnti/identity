using Identity.Tests.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests.Tests;

/// <summary>
/// Создаёт отдельную базу SQLite в памяти для интеграционного теста.
/// </summary>
public sealed class IdentitySqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    private IdentitySqliteTestDatabase(
        SqliteConnection connection,
        ServiceProvider serviceProvider)
    {
        _connection = connection;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Возвращает контейнер сервисов Identity, подключённый к тестовой базе.
    /// </summary>
    public IServiceProvider Services => _serviceProvider;

    /// <summary>
    /// Открывает новую изолированную базу SQLite и настраивает сервисы Identity для неё.
    /// </summary>
    /// <returns>Тестовая база данных SQLite в памяти.</returns>
    public static async Task<IdentitySqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        try
        {
            var serviceProvider = IdentityPersistenceTestServices.CreateForSqlite(connection);
            return new IdentitySqliteTestDatabase(connection, serviceProvider);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Освобождает сервисы и закрывает тестовую базу данных.
    /// </summary>
    /// <returns>Асинхронная операция освобождения ресурсов.</returns>
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
