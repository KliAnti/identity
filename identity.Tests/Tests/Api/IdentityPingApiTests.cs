using FluentAssertions;
using Identity.Grpc;
using Xunit;

namespace Identity.Tests.Tests.Api;

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
}
