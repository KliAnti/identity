using Grpc.Net.Client;
using Identity.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
    }

    /// <summary>
    /// gRPC-клиент Identity, используемый в API-тестах.
    /// </summary>
    public IdentityGrpcClient Client { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:Identity",
            "Host=localhost;Database=identity_tests;Username=identity;Password=not-used");
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
