using Identity.Infrastructure.Persistence;
using Identity.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TestFramework.Fixtures;
using TestFramework.Http;
using TestFramework.Postgres;
using Xunit;

namespace Identity.Tests.Tests;

/// <summary>
/// Поднимает Identity в памяти с реальной PostgreSQL-базой для HTTP integration-тестов.
/// </summary>
public sealed class IdentityApiFixture : MockFixtureBase<IdentityService>
{
    private readonly PostgresContainerFixture _postgres = new();
    private DatabaseResetter? _databaseResetter;

    /// <summary>Создаёт API-клиент с TestServer Identity.</summary>
    public ApiClient CreateIdentityApiClient() => CreateApiClient();

    /// <inheritdoc />
    protected override void ConfigureMocks(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<IdentityDbContext>>();
        services.RemoveAll<IdentityDbContext>();
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(_postgres.ConnectionString));
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:IdentityDb", _postgres.ConnectionString);

        base.ConfigureWebHost(builder);
    }

    /// <inheritdoc />
    public override async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using var dbContext = new IdentityDbContext(options);
        await dbContext.Database.MigrateAsync();

        _databaseResetter = new DatabaseResetter(_postgres.ConnectionString);
        await _databaseResetter.InitializeAsync();

        await Services.InitializeOpenIddictClientAsync();
    }

    /// <inheritdoc />
    public override async Task ResetAsync()
    {
        await base.ResetAsync();

        if (_databaseResetter is null)
            throw new InvalidOperationException("PostgreSQL resetter не инициализирован.");

        await _databaseResetter.ResetAsync();

        await using var dbContext = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .Options);

        await dbContext.Roles.AddRangeAsync(
            new Identity.Infrastructure.Persistence.Models.Role.RoleModel
            {
                Id = Guid.Parse("72c97b31-a2c5-4486-aee7-4115d650e102"),
                Name = "admin",
                NormalizedName = "ADMIN"
            },
            new Identity.Infrastructure.Persistence.Models.Role.RoleModel
            {
                Id = Guid.Parse("d1813e6f-c38a-43df-8728-bc345ab88f01"),
                Name = "user",
                NormalizedName = "USER"
            });

        await dbContext.SaveChangesAsync();
        await Services.InitializeOpenIddictClientAsync();
    }

    /// <inheritdoc />
    protected override async Task DisposeAsyncCore()
    {
        await _postgres.DisposeAsync();
    }
}

/// <summary>Объединяет Identity HTTP integration-тесты в общую fixture.</summary>
[CollectionDefinition(Name)]
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFixture>
{
    public const string Name = "Identity API";
}
