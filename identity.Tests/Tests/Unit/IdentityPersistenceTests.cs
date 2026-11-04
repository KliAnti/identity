using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Identity.Tests.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.Tests.Tests.Unit;

public sealed class IdentityPersistenceTests
{
    [Fact]
    public void AddIdentityPersistence_WhenConnectionStringIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddIdentityPersistence(configuration);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Не задана строка подключения 'ConnectionStrings:Identity'.");
    }

    [Fact]
    public void AddIdentityPersistence_WhenConnectionStringIsConfigured_RegistersPostgreSqlAndUniqueEmail()
    {
        // Arrange
        using var serviceProvider = IdentityPersistenceTestServices.Create(
            "Host=localhost;Database=identity_tests;Username=identity;Password=not-used");

        // Act
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var identityOptions = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert
        dbContext.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
        identityOptions.User.RequireUniqueEmail.Should().BeTrue();

        var emailIndex = dbContext.Model
            .FindEntityType(typeof(IdentityUserEntity))!
            .GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(IdentityUserEntity.NormalizedEmail)]));
        emailIndex.IsUnique.Should().BeTrue();
        emailIndex.GetDatabaseName().Should().Be("EmailIndex");
    }

    [Fact]
    public void GenerateMigrationScript_WhenNpgsqlIsConfigured_ContainsSchemaAndDefaultRoles()
    {
        // Arrange
        using var serviceProvider = IdentityPersistenceTestServices.Create(
            "Host=localhost;Database=identity_tests;Username=identity;Password=not-used");
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var migrator = dbContext.GetService<IMigrator>();

        // Act
        var script = migrator.GenerateScript();

        // Assert
        script.Should().Contain("CREATE TABLE \"AspNetUsers\"");
        script.Should().Contain("CREATE UNIQUE INDEX \"EmailIndex\"");
        script.Should().Contain("'admin'");
        script.Should().Contain("'user'");
    }
}
