using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Identity.Tests.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Identity.Tests.Tests.Integration;

/// <summary>
/// Проверяет создание схемы и сохранение данных Identity в SQLite.
/// </summary>
public sealed class IdentityPersistenceTests
{
    [Fact]
    public async Task EnsureCreated_WhenDatabaseIsEmpty_CreatesIdentitySchemaAndDefaultRoles()
    {
        // Arrange
        await using var database = await IdentitySqliteTestDatabase.CreateAsync();
        await using var scope = database.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // Act
        await dbContext.Database.EnsureCreatedAsync();
        var roleNames = await dbContext.Roles
            .OrderBy(role => role.Name)
            .Select(role => role.Name)
            .ToArrayAsync();

        // Assert
        roleNames.Should().Equal("admin", "user");
        (await dbContext.Database.CanConnectAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task CreateUser_WhenCalled_CanReadUserFromAnotherScope()
    {
        // Arrange
        await using var database = await IdentitySqliteTestDatabase.CreateAsync();
        string userId;
        IdentityResult createResult;

        await using (var setupScope = database.Services.CreateAsyncScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        // Act
        await using (var createScope = database.Services.CreateAsyncScope())
        {
            var userManager = createScope.ServiceProvider.GetRequiredService<UserManager<IdentityUserEntity>>();
            var user = new IdentityUserEntity
            {
                UserName = "persistent-user",
                Email = "Persistent.User@example.test"
            };
            createResult = await userManager.CreateAsync(user, "Strong-test-password-2026!");
            userId = user.Id;
        }

        await using var readScope = database.Services.CreateAsyncScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var savedUser = await readContext.Users.SingleOrDefaultAsync(user => user.Id == userId);

        // Assert
        createResult.Succeeded.Should().BeTrue(
            string.Join("; ", createResult.Errors.Select(error => error.Description)));
        savedUser.Should().NotBeNull();
        savedUser!.Email.Should().Be("Persistent.User@example.test");
        savedUser.NormalizedEmail.Should().Be("PERSISTENT.USER@EXAMPLE.TEST");
    }

    [Fact]
    public async Task SaveUser_WhenNormalizedEmailAlreadyExists_IsRejectedByDatabaseIndex()
    {
        // Arrange
        await using var database = await IdentitySqliteTestDatabase.CreateAsync();

        await using (var setupScope = database.Services.CreateAsyncScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Users.Add(new IdentityUserEntity
            {
                Id = "first-user",
                UserName = "first-user",
                NormalizedUserName = "FIRST-USER",
                Email = "duplicate@example.test",
                NormalizedEmail = "DUPLICATE@EXAMPLE.TEST"
            });
            await dbContext.SaveChangesAsync();
        }

        await using var duplicateScope = database.Services.CreateAsyncScope();
        var duplicateContext = duplicateScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        duplicateContext.Users.Add(new IdentityUserEntity
        {
            Id = "second-user",
            UserName = "second-user",
            NormalizedUserName = "SECOND-USER",
            Email = "DUPLICATE@example.test",
            NormalizedEmail = "DUPLICATE@EXAMPLE.TEST"
        });

        // Act
        var act = () => duplicateContext.SaveChangesAsync();
        var exception = await act.Should().ThrowAsync<DbUpdateException>();

        // Assert
        exception.Which.InnerException.Should().BeOfType<SqliteException>()
            .Which.SqliteExtendedErrorCode.Should().Be(2067);
    }

    [Fact]
    public async Task CreateUser_WhenEmailAlreadyExistsWithDifferentCasing_IsRejected()
    {
        // Arrange
        await using var database = await IdentitySqliteTestDatabase.CreateAsync();
        await using var scope = database.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUserEntity>>();

        var firstUser = new IdentityUserEntity
        {
            UserName = "first-user",
            Email = "person@example.test"
        };
        var firstResult = await userManager.CreateAsync(firstUser, "Strong-test-password-2026!");

        // Act
        var duplicateUser = new IdentityUserEntity
        {
            UserName = "second-user",
            Email = "PERSON@example.test"
        };
        var duplicateResult = await userManager.CreateAsync(duplicateUser, "Strong-test-password-2026!");

        // Assert
        firstResult.Succeeded.Should().BeTrue();
        duplicateResult.Succeeded.Should().BeFalse();
        duplicateResult.Errors.Should().Contain(error => error.Code == "DuplicateEmail");
        (await dbContext.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateUser_WhenPasswordIsTooWeak_IsRejectedWithoutSavingUser()
    {
        // Arrange
        await using var database = await IdentitySqliteTestDatabase.CreateAsync();
        await using var scope = database.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUserEntity>>();
        var user = new IdentityUserEntity
        {
            UserName = "weak-password-user",
            Email = "weak-password@example.test"
        };

        // Act
        var result = await userManager.CreateAsync(user, "weak");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Code == "PasswordTooShort");
        (await dbContext.Users.AnyAsync(savedUser => savedUser.UserName == user.UserName)).Should().BeFalse();
    }
}
