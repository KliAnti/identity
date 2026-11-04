using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

namespace Identity.Tests.Tests.Helpers;

internal static class IdentityPersistenceTestServices
{
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
