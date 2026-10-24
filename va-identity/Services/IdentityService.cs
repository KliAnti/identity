using Grpc.Core;
using va_identity.Grpc;

namespace va_identity.Services;

/// <summary>
/// Реализует методы gRPC-сервиса Identity.
/// </summary>
/// <param name="logger">Логгер для записи событий сервиса.</param>
public sealed class IdentityService(ILogger<IdentityService> logger) : Identity.IdentityBase
{
    /// <summary>
    /// Обрабатывает запрос Ping, записывает полученное сообщение в лог и возвращает Pong.
    /// </summary>
    /// <param name="request">Входящий запрос с сообщением.</param>
    /// <param name="context">Контекст текущего gRPC-вызова.</param>
    /// <returns>Ответ PongResponse с сообщением «Pong».</returns>
    public override Task<PongResponse> Ping(PingRequest request, ServerCallContext context)
    {
        logger.LogInformation("Received ping: {Message}", request.Message);

        return Task.FromResult(new PongResponse { Message = "Pong" });
    }
}
