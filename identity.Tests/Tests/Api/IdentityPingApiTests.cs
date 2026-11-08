using FluentAssertions;
using Identity.Grpc;
using Xunit;

namespace Identity.Tests.Tests.Api;

/// <summary>
/// Проверяет gRPC API Identity на запрос Ping.
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class IdentityPingApiTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Ping_WhenMessageIsValid_ReturnsPong()
    {
        // Arrange
        var request = new PingRequest { Message = "Ping" };

        // Act
        var response = await fixture.Client.PingAsync(request);

        // Assert
        response.Message.Should().Be("Pong");
    }

    [Fact]
    public async Task Ping_WhenMessageIsNotPing_ReturnsPong()
    {
        // Arrange
        var request = new PingRequest { Message = "32132132132132" };

        // Act
        var response = await fixture.Client.PingAsync(request);

        // Assert
        response.Message.Should().Be( "Pong" );
    }
}
