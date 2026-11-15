using Grpc.Net.Client;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Models.Role;
using Identity.Infrastructure.Persistence.Models.User;
using Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using IdentityGrpcClient = global::Identity.Grpc.Identity.IdentityClient;

namespace Identity.Tests.Tests;

/// <summary>
/// Запускает Identity в памяти и предоставляет gRPC-клиент для API-тестов.
/// </summary>
public sealed class IdentityApiFixture : WebApplicationFactory<IdentityService>
{
    /// <summary>
    /// Базовый адрес тестового хоста Identity.
    /// </summary>
    public const string BaseAddress = "http://localhost";

    private readonly GrpcChannel _channel;

    /// <summary>
    /// Создаёт тестовый хост Identity в памяти и инициализирует gRPC-клиент.
    /// </summary>
    public IdentityApiFixture()
    {
        _channel = GrpcChannel.ForAddress(
            BaseAddress,
            new GrpcChannelOptions { HttpHandler = Server.CreateHandler() });
        Client = new IdentityGrpcClient(_channel);

        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreated();
        Services.InitializeOpenIddictClientAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// gRPC-клиент Identity, используемый в API-тестах.
    /// </summary>
    public IdentityGrpcClient Client { get; }

    public async Task<UserModel> CreateUserAsync(
        string email,
        string password,
        bool emailConfirmed,
        params string[] roles)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserModel>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<RoleModel>>();
        var user = new UserModel
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = emailConfirmed
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
        }

        foreach (var role in roles)
        {
            if (await roleManager.FindByNameAsync(role) is null)
            {
                var roleResult = await roleManager.CreateAsync(new RoleModel
                {
                    Id = Guid.NewGuid(),
                    Name = role,
                    NormalizedName = role.ToUpperInvariant()
                });
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join(", ", roleResult.Errors.Select(error => error.Description)));
                }
            }

            var roleAssignment = await userManager.AddToRoleAsync(user, role);
            if (!roleAssignment.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", roleAssignment.Errors.Select(error => error.Description)));
            }
        }

        return user;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:IdentityDb",
            "Host=localhost;Database=identity_tests;Username=identity;Password=not-used");
        builder.UseSetting("OpenIddict:InitializeClient", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<IdentityDbContext>>();
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            services.RemoveAll<IdentityDbContext>();
            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase("identity-tests"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _channel.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Определяет xUnit-коллекцию тестов Identity API с общей фикстурой.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFixture>
{
    /// <summary>
    /// Имя xUnit-коллекции для тестов Identity API.
    /// </summary>
    public const string Name = "Identity API";
}
