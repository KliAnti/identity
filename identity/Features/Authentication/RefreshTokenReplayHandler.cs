using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Identity.Features.Authentication;

public sealed class RefreshTokenReplayHandler(SessionRevocationService sessionRevocation)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessAuthenticationContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessAuthenticationContext context)
    {
        if (context.Request is null || !context.Request.IsRefreshTokenGrantType())
            return;

        await sessionRevocation.RevokeReusedRefreshTokenAsync(
            context.RefreshToken,
            context.CancellationToken);
    }
}
