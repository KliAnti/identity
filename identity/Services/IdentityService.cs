using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Grpc.Core;
using identity.Grpc;
using Identity.Features.Authentication;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenIddict.Abstractions;
using RegistrationRpcRequest = identity.Grpc.RegisterRequest;
using RegistrationRpcResponse = identity.Grpc.RegisterResponse;

namespace Identity.Services;

/// <summary>
/// Реализует методы gRPC-сервиса Identity.
/// </summary>
/// <param name="logger">Логгер для записи событий сервиса.</param>
public sealed class IdentityService(
    ILogger<IdentityService> logger,
    UserManager<UserModel> userManager,
    IdentityDbContext dbContext,
    SessionRevocationService sessionRevocation) : global::identity.Grpc.Identity.IdentityBase
{
    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    /// <summary>
    /// Обрабатывает запрос Ping, записывает полученное сообщение в лог и возвращает Pong.
    /// </summary>
    /// <param name="request">Входящий запрос с сообщением.</param>
    /// <param name="context">Контекст текущего gRPC-вызова.</param>
    /// <returns>Ответ PongResponse с сообщением «Pong».</returns>
    public override Task<PongResponse> Ping(PingRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation("Received ping: {Message}", request.Message);

        return Task.FromResult(new PongResponse { Message = "Pong" });
    }

    /// <summary>Создаёт пользовательскую учётную запись.</summary>
    public override async Task<RegistrationRpcResponse> Register(
        RegistrationRpcRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !EmailAddressValidator.IsValid(email))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A valid email address is required."));

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "A password between 1 and 1024 characters is required."));

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
            throw new RpcException(new Status(StatusCode.AlreadyExists, "email_already_registered"));

        var user = new UserModel
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = userManager.NormalizeEmail(email) ?? email.ToUpperInvariant(),
            EmailConfirmed = true
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(context.CancellationToken);

        try
        {
            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                throw new RpcException(new Status(StatusCode.InvalidArgument, FormatIdentityErrors(result)));

            var roleResult = await userManager.AddToRoleAsync(user, "user");
            if (!roleResult.Succeeded)
                throw new RpcException(new Status(StatusCode.InvalidArgument, FormatIdentityErrors(roleResult)));

            await transaction.CommitAsync(context.CancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, "email_already_registered"));
        }

        return new RegistrationRpcResponse { UserId = user.Id.ToString(), Email = user.Email };
    }

    /// <summary>Отзывает сессию текущего устройства.</summary>
    [Authorize]
    public override async Task<LogoutResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = context.GetHttpContext().User;
        var subject = user.FindFirstValue(OpenIddictConstants.Claims.Subject);
        var sid = user.FindFirstValue("sid");

        if (!Guid.TryParse(subject, out var userId) || !Guid.TryParse(sid, out var sessionId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "The user or session is invalid."));

        var session = await dbContext.Sessions
            .SingleOrDefaultAsync(candidate => candidate.Id == sessionId, context.CancellationToken);

        if (session is null || session.RevokedAt is not null)
            return new LogoutResponse();

        var device = await dbContext.Devices
            .SingleOrDefaultAsync(candidate => candidate.Id == session.DeviceId, context.CancellationToken);

        if (device is null || device.UserId != userId)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "The session does not belong to the user."));

        await sessionRevocation.RevokeSessionAsync(sessionId, subject, context.CancellationToken);
        return new LogoutResponse();
    }

    /// <summary>Отзывает все сессии и токены пользователя.</summary>
    [Authorize]
    public override async Task<LogoutResponse> LogoutAll(LogoutRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = context.GetHttpContext(). User;
        var subject = user.FindFirstValue(OpenIddictConstants.Claims.Subject);
        if (!Guid.TryParse(subject, out var userId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "The user is invalid."));

        await sessionRevocation.RevokeAllForUserAsync(userId, subject, context.CancellationToken);
        return new LogoutResponse();
    }

    private static string FormatIdentityErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
