using System.Security.Claims;
using System.Text.Json;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Identity.Features.Authentication;

public sealed class SessionRevocationService(
    IdentityDbContext dbContext,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictTokenManager tokenManager)
{
    public async Task RevokeSessionAsync(Guid sessionId, string subject, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var revokedAt = DateTimeOffset.UtcNow;
        await dbContext.Sessions
            .Where(candidate => candidate.Id == sessionId && candidate.RevokedAt == null)
            .ExecuteUpdateAsync(candidate => candidate
                .SetProperty(session => session.RevokedAt, _ => revokedAt), cancellationToken);

        await foreach (var authorization in authorizationManager.FindBySubjectAsync(subject, cancellationToken))
        {
            var properties = await authorizationManager.GetPropertiesAsync(authorization, cancellationToken);
            if (!properties.TryGetValue("sid", out var value)
                || value.ValueKind != JsonValueKind.String
                || value.GetString() != sessionId.ToString())
                continue;

            var authorizationId = await authorizationManager.GetIdAsync(authorization, cancellationToken);
            if (!string.IsNullOrEmpty(authorizationId))
                await tokenManager.RevokeByAuthorizationIdAsync(authorizationId, cancellationToken);

            await authorizationManager.TryRevokeAsync(authorization, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string subject, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await tokenManager.RevokeBySubjectAsync(subject, cancellationToken);
        await authorizationManager.RevokeBySubjectAsync(subject, cancellationToken);

        var revokedAt = DateTimeOffset.UtcNow;
        await dbContext.Sessions
            .Where(candidate => candidate.RevokedAt == null)
            .Join(dbContext.Devices,
                session => session.DeviceId,
                device => device.Id,
                (session, device) => new { session, device })
            .Where(candidate => candidate.device.UserId == userId)
            .Select(candidate => candidate.session)
            .ExecuteUpdateAsync(candidate => candidate
                .SetProperty(session => session.RevokedAt, _ => revokedAt), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeReusedRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return;

        var token = await tokenManager.FindByReferenceIdAsync(refreshToken, cancellationToken);
        if (token is null)
            return;

        var redemptionDate = await tokenManager.GetRedemptionDateAsync(token, cancellationToken);
        var status = await tokenManager.GetStatusAsync(token, cancellationToken);
        if (redemptionDate is null && status != OpenIddictConstants.Statuses.Redeemed)
            return;

        var subject = await tokenManager.GetSubjectAsync(token, cancellationToken);
        var authorizationId = await tokenManager.GetAuthorizationIdAsync(token, cancellationToken);
        if (!Guid.TryParse(authorizationId, out var parsedAuthorizationId))
            return;

        var session = await dbContext.Sessions
            .SingleOrDefaultAsync(candidate => candidate.AuthorizationId == parsedAuthorizationId, cancellationToken);

        if (session is null)
            return;

        await RevokeSessionAsync(session.Id, subject ?? string.Empty, cancellationToken);
    }
}
