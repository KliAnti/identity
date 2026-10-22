using Grpc.Core;
using va_identity.Grpc;

namespace va_identity.Services;

public sealed class IdentityService(ILogger<IdentityService> logger) : Identity.IdentityBase
{
    public override Task<PongResponse> Ping(PingRequest request, ServerCallContext context)
    {
        logger.LogInformation("Received ping: {Message}", request.Message);

        return Task.FromResult(new PongResponse { Message = "Pong" });
    }
}
