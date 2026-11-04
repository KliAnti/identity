using System.Collections.Concurrent;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using va_identity.Grpc;
using va_identity.Services;
using Xunit;

namespace va_identity.Tests;

/// <summary>
/// Интеграционные тесты метода Ping сервиса Identity по gRPC.
/// </summary>
public sealed class IdentityPingTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly GrpcChannel _channel;

    /// <summary>
    /// Создаёт gRPC-канал, подключённый к тестовому серверу в памяти.
    /// </summary>
    /// <param name="factory">Фабрика тестового сервера.</param>
    public IdentityPingTests(WebApplicationFactory<Program> factory)
    {
        _channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() });
    }

    /// <summary>
    /// Проверяет, что Ping возвращает ответ Pong для переданного сообщения.
    /// </summary>
    /// <param name="message">Сообщение в запросе Ping.</param>
    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    public async Task Ping_returns_pong_response(string message)
    {
        var client = new Identity.IdentityClient(_channel);

        var response = await client.PingAsync(new PingRequest { Message = message });

        Assert.Equal("Pong", response.Message);
    }

    /// <summary>
    /// Проверяет, что Ping записывает входящее сообщение в лог на уровне Information.
    /// </summary>
    [Fact]
    public async Task Ping_logs_incoming_request()
    {
        var provider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(provider));
        var service = new IdentityService(loggerFactory.CreateLogger<IdentityService>());
        const string incomingMessage = "test ping";

        await service.Ping(new PingRequest { Message = incomingMessage }, null!);

        var logEntry = Assert.Single(provider.Entries);
        Assert.Equal(LogLevel.Information, logEntry.Level);
        Assert.Equal($"Received ping: {incomingMessage}", logEntry.Message);
        Assert.Contains(
            logEntry.Properties,
            property => property.Key == "Message" && Equals(property.Value, incomingMessage));
    }

    public void Dispose() => _channel.Dispose();

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<CapturedLogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToArray()
                : Array.Empty<KeyValuePair<string, object?>>();

            entries.Enqueue(new CapturedLogEntry(logLevel, formatter(state, exception), properties));
        }
    }

    private sealed record CapturedLogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyList<KeyValuePair<string, object?>> Properties);
}
