using System.Security.Claims;
using System.Text.Json;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Models.Device;
using Identity.Infrastructure.Persistence.Models.Session;
using Identity.Infrastructure.Persistence.Models.User;
using Identity.Features.Authentication.LoginAttemptRateLimiting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace Identity.Features.Authentication;

/// <summary>
/// Подключает OAuth password-token endpoint для проверки учётных данных и выпуска access token.
/// </summary>
public static class PasswordTokenEndpoint
{
    private const string InvalidCredentialsDescription = "The email or password is invalid.";

    /// <summary>
    /// Регистрирует POST-маршрут <c>/connect/token</c> для выдачи токенов OpenIddict.
    /// </summary>
    /// <param name="endpoints">Маршрутизатор ASP.NET Core, к которому добавляется endpoint.</param>
    /// <returns>Тот же маршрутизатор для продолжения настройки.</returns>
    /// <remarks>Запрос должен передаваться по HTTPS вне Development и Testing.</remarks>
    public static IEndpointRouteBuilder MapPasswordTokenEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/token", HandleAsync);
        return endpoints;
    }

    /// <summary>
    /// Проверяет парольный grant, регистрирует устройство и сессию, затем формирует access token.
    /// </summary>
    /// <param name="context">HTTP-контекст с разобранным OpenIddict-запросом и заголовками ответа.</param>
    /// <param name="userManager">Хранилище пользователей для проверки email, пароля и подтверждения адреса.</param>
    /// <param name="dbContext">Контекст для сохранения устройства и сессии, связанной с токеном.</param>
    /// <param name="scopeManager">Хранилище scope, из которого определяется audience токена.</param>
    /// <param name="authorizationManager">Менеджер авторизаций для привязки сессии.</param>
    /// <param name="tokenManager">Менеджер токенов для работы с refresh-токенами.</param>
    /// <param name="sessionRevocation">Служба отзыва сессий.</param>
    /// <param name="openIddictOptions">Настройки OpenIddict, включая срок действия access token.</param>
    /// <param name="rateLimiter">Ограничитель числа попыток входа для нормализованного email.</param>
    /// <param name="cancellationToken">Токен отмены HTTP-запроса, передаваемый операциям базы данных.</param>
    /// <returns>Результат OpenIddict с access token либо OAuth-ошибкой; при превышении лимита возвращает HTTP 429.</returns>
    /// <remarks>
    /// Запрос должен содержать scope <c>api</c> и GUID <c>device_id</c>. Неверные или неподтверждённые
    /// учётные данные возвращаются одной ошибкой, чтобы не раскрывать состояние аккаунта.
    /// </remarks>
    private static async Task<IResult> HandleAsync(
        HttpContext context,
        UserManager<UserModel> userManager,
        IdentityDbContext dbContext,
        IOpenIddictScopeManager scopeManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictTokenManager tokenManager,
        SessionRevocationService sessionRevocation,
        IOptionsMonitor<OpenIddictServerOptions> openIddictOptions,
        LoginAttemptRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("The OpenID Connect request could not be retrieved.");

        if (request.IsRefreshTokenGrantType())
            return await HandleRefreshTokenGrantAsync(
                context, request, dbContext, sessionRevocation, openIddictOptions, cancellationToken);

        return await HandlePasswordGrantAsync(
            context, request, userManager, dbContext, scopeManager, authorizationManager, openIddictOptions,
            rateLimiter, cancellationToken);
    }

    private static async Task<IResult> HandleRefreshTokenGrantAsync(
        HttpContext context,
        OpenIddictRequest request,
        IdentityDbContext dbContext,
        SessionRevocationService sessionRevocation,
        IOptionsMonitor<OpenIddictServerOptions> openIddictOptions,
        CancellationToken cancellationToken)
    {
        var submittedRefreshToken = request.RefreshToken;

        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
            return Forbid(OpenIddictConstants.Errors.InvalidGrant, "The refresh token is invalid or revoked.");

        var subject = result.Principal.GetClaim(OpenIddictConstants.Claims.Subject);
        var sid = result.Principal.GetClaim("sid");
        var deviceClaim = result.Principal.GetClaim("device_id");
        var refreshTokenClaim = result.Principal.GetClaim("rt_id");

        if (!Guid.TryParse(sid, out var refreshSessionId)
            || !Guid.TryParse(deviceClaim, out var refreshDeviceId)
            || !Guid.TryParse(subject, out var refreshUserId)
            || !Guid.TryParse(refreshTokenClaim, out var refreshTokenId))
            return Forbid(OpenIddictConstants.Errors.InvalidGrant, "The refresh token is invalid or revoked.");

        var refreshNow = DateTimeOffset.UtcNow;
        var refreshLifetime = openIddictOptions.CurrentValue.RefreshTokenLifetime
            ?? throw new InvalidOperationException("The OpenIddict refresh token lifetime is not configured.");
        var nextRefreshTokenId = Guid.NewGuid();

        var refreshDevice = await dbContext.Devices
            .SingleOrDefaultAsync(candidate => candidate.Id == refreshDeviceId
                && candidate.UserId == refreshUserId
                && candidate.RevokedAt == null, cancellationToken);

        if (refreshDevice is null)
            return Forbid(OpenIddictConstants.Errors.InvalidGrant, "The refresh token is invalid or revoked.");

        var rotated = await dbContext.Sessions
            .Where(candidate => candidate.Id == refreshSessionId
                && candidate.DeviceId == refreshDeviceId
                && candidate.RevokedAt == null
                && candidate.RefreshTokenId == refreshTokenId
                && candidate.ExpiresAt > refreshNow)
            .ExecuteUpdateAsync(candidate => candidate
                .SetProperty(session => session.RefreshTokenId, _ => nextRefreshTokenId)
                .SetProperty(session => session.ExpiresAt, _ => refreshNow.Add(refreshLifetime)), cancellationToken);

        if (rotated != 1)
        {
            await sessionRevocation.RevokeSessionAsync(refreshSessionId, subject ?? string.Empty, cancellationToken);
            return Forbid(OpenIddictConstants.Errors.InvalidGrant, "The refresh token is invalid or revoked.");
        }

        refreshDevice.LastSeenAt = refreshNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var refreshPrincipal = result.Principal;
        refreshPrincipal.SetScopes(request.GetScopes());
        refreshPrincipal.SetClaim("rt_id", nextRefreshTokenId.ToString());
        refreshPrincipal.SetExpirationDate(refreshNow.Add(openIddictOptions.CurrentValue.AccessTokenLifetime
            ?? throw new InvalidOperationException("The OpenIddict access token lifetime is not configured.")));

        return Results.SignIn(
            refreshPrincipal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> HandlePasswordGrantAsync(
        HttpContext context,
        OpenIddictRequest request,
        UserManager<UserModel> userManager,
        IdentityDbContext dbContext,
        IOpenIddictScopeManager scopeManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IOptionsMonitor<OpenIddictServerOptions> openIddictOptions,
        LoginAttemptRateLimiter rateLimiter,
        CancellationToken cancellationToken)
    {
        if (request.IsPasswordGrantType()
            && (string.IsNullOrWhiteSpace(request.Username)
                || string.IsNullOrEmpty(request.Password)
                || request.Username.Length > 256
                || request.Password.Length > 1024))
            return Forbid(OpenIddictConstants.Errors.InvalidRequest, "The login request is invalid.");

        if (request.IsPasswordGrantType()
            && !request.GetScopes().Contains(IdentityOpenIddictDefaults.ApiScope, StringComparer.Ordinal))
            return Forbid(OpenIddictConstants.Errors.InvalidScope, "The requested scope is not allowed.");

        if (!Guid.TryParse(request.GetParameter("device_id").ToString(), out var deviceId))
            return Forbid(OpenIddictConstants.Errors.InvalidRequest, "A valid device_id is required.");

        var email = request.Username!.Trim();
        var normalizedEmail = userManager.NormalizeEmail(email) ?? email.ToUpperInvariant();
        var rateLimit = rateLimiter.TryAcquire(normalizedEmail);
        if (!rateLimit.IsAllowed)
        {
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(rateLimit.RetryAfter.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.Json(
                new
                {
                    error = OpenIddictConstants.Errors.TemporarilyUnavailable,
                    error_description = "Too many login attempts. Try again later."
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return InvalidCredentials();

        var password = request.Password!;
        var hasPassword = await userManager.HasPasswordAsync(user);
        var passwordIsValid = hasPassword && await userManager.CheckPasswordAsync(user, password);

        var emailIsConfirmed = await userManager.IsEmailConfirmedAsync(user);
        if (!passwordIsValid || !emailIsConfirmed)
            return InvalidCredentials();

        var roles = await userManager.GetRolesAsync(user);

        var now = DateTimeOffset.UtcNow;
        var device = await dbContext.Devices.SingleOrDefaultAsync(
            candidate => candidate.Id == deviceId,
            cancellationToken);

        if (device is not null && (device.UserId != user.Id || device.RevokedAt is not null))
            return InvalidCredentials();

        if (device is null)
        {
            device = new DeviceModel
            {
                Id = deviceId,
                UserId = user.Id,
                CreatedAt = now,
                LastSeenAt = now
            };
            dbContext.Devices.Add(device);
        }
        else
        {
            device.LastSeenAt = now;
        }

        var loginRefreshTokenId = Guid.NewGuid();
        var session = new SessionModel
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            CreatedAt = now,
            ExpiresAt = now.Add(openIddictOptions.CurrentValue.RefreshTokenLifetime
                ?? throw new InvalidOperationException("The OpenIddict refresh token lifetime is not configured.")),
            RefreshTokenId = loginRefreshTokenId
        };
        dbContext.Sessions.Add(session);

        var authorization = await authorizationManager.CreateAsync(
            new OpenIddictAuthorizationDescriptor
            {
                Subject = user.Id.ToString(),
                Status = OpenIddictConstants.Statuses.Valid,
                Type = OpenIddictConstants.AuthorizationTypes.Permanent,
                Scopes = { IdentityOpenIddictDefaults.ApiScope, "offline_access" },
                Properties =
                {
                    ["sid"] = JsonSerializer.SerializeToElement(session.Id.ToString()),
                    ["device_id"] = JsonSerializer.SerializeToElement(device.Id.ToString())
                }
            },
            cancellationToken);

        var authorizationId = await authorizationManager.GetIdAsync(authorization, cancellationToken);
        if (Guid.TryParse(authorizationId, out var parsedAuthorizationId))
            session.AuthorizationId = parsedAuthorizationId;

        await dbContext.SaveChangesAsync(cancellationToken);

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, OpenIddictConstants.Claims.Name, OpenIddictConstants.Claims.Role);
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.JwtId, Guid.NewGuid().ToString("N")));
        identity.AddClaim(new Claim("sid", session.Id.ToString()));
        identity.AddClaim(new Claim("device_id", device.Id.ToString()));
        identity.AddClaim(new Claim("rt_id", loginRefreshTokenId.ToString()));

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role));
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization, cancellationToken));
        principal.SetExpirationDate(session.ExpiresAt);
        principal.SetScopes(request.GetScopes().Append("offline_access"));

        var resources = new List<string>();
        await foreach (var resource in scopeManager.ListResourcesAsync(principal.GetScopes(), cancellationToken))
        {
            resources.Add(resource);
        }

        principal.SetResources(resources);
        principal.SetDestinations(static claim =>
            claim.Type is OpenIddictConstants.Claims.Subject or
                OpenIddictConstants.Claims.JwtId or
                "sid" or
                "device_id" or
                "rt_id" or
                OpenIddictConstants.Claims.Role
                ? [OpenIddictConstants.Destinations.AccessToken]
                : []);

        return Results.SignIn(
            principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IResult InvalidCredentials() =>
        Forbid(OpenIddictConstants.Errors.InvalidGrant, InvalidCredentialsDescription);

    private static IResult Forbid(string error, string description) =>
        Results.Forbid(
            authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }));
}