using FluentAssertions;
using Microsoft.Extensions.Logging;
using Identity.Grpc;
using Identity.Services;
using Identity.Tests.Tests.Helpers;
using Xunit;

namespace Identity.Tests.Tests.Unit;

public sealed class IdentityPingTests
{
    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData(" ")]
    public async Task Ping_WhenMessageIsEmptyNumericOrWhitespace_ReturnsPong(string message)
    {
        // Arrange
        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(loggerProvider));
        var service = new IdentityService(loggerFactory.CreateLogger<IdentityService>());
        var request = new PingRequest { Message = message };

        // Act
        var response = await service.Ping(request, null!);

        // Assert
        response.Message.Should().Be("Pong");
    }

    [Fact]
    public async Task Ping_WhenRequestIsValid_LogsIncomingMessage()
    {
        // Arrange
        using var loggerProvider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(loggerProvider));
        var service = new IdentityService(loggerFactory.CreateLogger<IdentityService>());
        const string message = "Ping";

        // Act
        await service.Ping(new PingRequest { Message = message }, null!);

        // Assert
        var logEntry = loggerProvider.Entries.Should().ContainSingle().Which;
        logEntry.Level.Should().Be(LogLevel.Information);
        logEntry.Message.Should().Be($"Received ping: {message}");
        logEntry.Properties.Should().Contain(property =>
            property.Key == "Message" && Equals(property.Value, message));
    }

    [Fact]
    public async Task Ping_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var service = new IdentityService(loggerFactory.CreateLogger<IdentityService>());

        // Act
        var act = () => service.Ping(null!, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
