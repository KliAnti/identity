using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using IdentityGrpcClient = global::Identity.Grpc.Identity.IdentityClient;

namespace Identity.Tests.Tests;

/// <summary>
/// Hosts Identity in memory and exposes its gRPC client for API tests.
/// </summary>
public sealed class IdentityApiFixture : WebApplicationFactory<Program>
{
    public const string BaseAddress = "http://localhost";

    private readonly GrpcChannel _channel;

    public IdentityApiFixture()
    {
        _channel = GrpcChannel.ForAddress(
            BaseAddress,
            new GrpcChannelOptions { HttpHandler = Server.CreateHandler() });
        Client = new IdentityGrpcClient(_channel);
    }

    public IdentityGrpcClient Client { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _channel.Dispose();
        }

        base.Dispose(disposing);
    }
}

[CollectionDefinition(Name)]
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFixture>
{
    public const string Name = "Identity API";
}
