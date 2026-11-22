using System.Security.Claims;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace Identity.Features.Authentication;

/// <summary>
/// Подключает операции выхода из текущей сессии и со всех устройств.
/// </summary>
public static class LogoutEndpoints
{
    /// <summary>Регистрирует logout endpoint'ы.</summary>
    public static IEndpointRouteBuilder MapLogoutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var logout = endpoints.MapPost("/logout", LogoutCurrentAsync)
            .RequireAuthorization();

        var logoutAll = endpoints.MapPost("/logout/all", LogoutAllAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> LogoutCurrentAsync(
        ClaimsPrincipal user,
        IdentityDbContext dbContext,
        SessionRevocationService sessionRevocation,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue(OpenIddictConstants.Claims.Subject);
        var sid = user.FindFirstValue("sid");

        if (!Guid.TryParse(subject, out var userId) || !Guid.TryParse(sid, out var sessionId))
            return Results.Unauthorized();

        var session = await dbContext.Sessions
            .SingleOrDefaultAsync(candidate => candidate.Id == sessionId, cancellationToken);

        if (session is null || session.RevokedAt is not null)
            return Results.NoContent();

        var device = await dbContext.Devices
            .SingleOrDefaultAsync(candidate => candidate.Id == session.DeviceId, cancellationToken);

        if (device is null || device.UserId != userId)
            return Results.Unauthorized();

        await sessionRevocation.RevokeSessionAsync(sessionId, subject, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        ClaimsPrincipal user,
        SessionRevocationService sessionRevocation,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue(OpenIddictConstants.Claims.Subject);
        if (!Guid.TryParse(subject, out var userId))
            return Results.Unauthorized();

        await sessionRevocation.RevokeAllForUserAsync(userId, subject, cancellationToken);
        return Results.NoContent();
    }
}
