using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Models.Session;
using Identity.Tests.Tests.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.Tests.Tests.Api;

/// <summary>
/// Проверяет ротацию refresh-токенов, обнаружение повторного использования и logout-сессии.
/// </summary>
[Collection(IdentityApiCollection.Name)]
public sealed class AuthenticationTests : IAsyncLifetime
{
    private readonly IdentityApiFixture _fixture;
    private readonly IdentityApi _identityApi;

    public AuthenticationTests(IdentityApiFixture fixture)
    {
        _fixture = fixture;
        _identityApi = new IdentityApi(fixture.CreateIdentityApiClient());
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Refresh_WhenTokenIsValid_RotatesRefreshToken()
    {
        var login = await LoginAsync(Guid.NewGuid());
        login.Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResponse = await _identityApi.Refresh(login.RefreshToken);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<TokenResponse>();
        rotated.Should().NotBeNull();
        rotated!.AccessToken.Should().NotBeNullOrWhiteSpace();
        rotated.RefreshToken.Should().NotBeNullOrWhiteSpace();
        rotated.RefreshToken.Should().NotBe(login.RefreshToken);

        var reuse = await _identityApi.Refresh(login.RefreshToken);
        reuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_WhenTokenIsReused_RevokesOnlyItsDeviceSession()
    {
        var deviceOne = Guid.NewGuid();
        var deviceTwo = Guid.NewGuid();
        var email = $"user-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!";
        await RegisterAsync(email, password);
        var loginOne = await LoginAsync(deviceOne, email, password);
        var loginTwo = await LoginAsync(deviceTwo, email, password);

        var firstRotation = await _identityApi.Refresh(loginOne.RefreshToken);
        firstRotation.StatusCode.Should().Be(HttpStatusCode.OK);

        var reuse = await _identityApi.Refresh(loginOne.RefreshToken);
        reuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var revokedSession = await dbContext.Sessions
            .SingleAsync(candidate => candidate.DeviceId == deviceOne);
        revokedSession.RevokedAt.Should().NotBeNull();

        var otherSession = await dbContext.Sessions
            .SingleAsync(candidate => candidate.DeviceId == deviceTwo);
        otherSession.RevokedAt.Should().BeNull();

        var deviceTwoRefresh = await _identityApi.Refresh(loginTwo.RefreshToken);
        deviceTwoRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WhenTokenIsUnknown_RejectsWithoutIssuingPair()
    {
        var response = await _identityApi.Refresh("unknown-refresh-token");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        body!.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Logout_WhenCurrentDeviceIsUsed_RevokesOnlyCurrentSession()
    {
        var deviceOne = Guid.NewGuid();
        var deviceTwo = Guid.NewGuid();
        var email = $"user-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!";
        await RegisterAsync(email, password);
        var loginOne = await LoginAsync(deviceOne, email, password);
        var loginTwo = await LoginAsync(deviceTwo, email, password);

        var logout = await _identityApi.Logout(loginOne.AccessToken);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var current = await dbContext.Sessions.SingleAsync(candidate => candidate.DeviceId == deviceOne);
        current.RevokedAt.Should().NotBeNull();

        var other = await dbContext.Sessions.SingleAsync(candidate => candidate.DeviceId == deviceTwo);
        other.RevokedAt.Should().BeNull();

        var refreshOtherDevice = await _identityApi.Refresh(loginTwo.RefreshToken);
        refreshOtherDevice.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LogoutAll_WhenUserIsAuthenticated_RevokesEverySession()
    {
        var deviceOne = Guid.NewGuid();
        var deviceTwo = Guid.NewGuid();
        var email = $"user-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!";
        await RegisterAsync(email, password);
        var loginOne = await LoginAsync(deviceOne, email, password);
        await LoginAsync(deviceTwo, email, password);

        var logout = await _identityApi.LogoutAll(loginOne.AccessToken);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var sessions = await dbContext.Sessions.ToListAsync();
        sessions.Should().HaveCount(2);
        sessions.Should().OnlyContain(candidate => candidate.RevokedAt != null);
    }

    [Fact]
    public async Task Refresh_WhenSessionIsExpired_RejectsToken()
    {
        var deviceId = Guid.NewGuid();
        var login = await LoginAsync(deviceId);

        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var session = await dbContext.Sessions.SingleAsync(candidate => candidate.DeviceId == deviceId);
            session.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await dbContext.SaveChangesAsync();
        }

        var response = await _identityApi.Refresh(login.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        body!.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Refresh_WhenSessionIsRevoked_RejectsToken()
    {
        var deviceId = Guid.NewGuid();
        var login = await LoginAsync(deviceId);

        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var session = await dbContext.Sessions.SingleAsync(candidate => candidate.DeviceId == deviceId);
            session.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        var response = await _identityApi.Refresh(login.RefreshToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<OAuthErrorResponse>();
        body!.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Refresh_WhenSubmittedConcurrently_IssuesOnlyOnePair()
    {
        var login = await LoginAsync(Guid.NewGuid());

        var first = _identityApi.Refresh(login.RefreshToken);
        var second = _identityApi.Refresh(login.RefreshToken);
        var responses = await Task.WhenAll(first, second);

        responses.Count(candidate => candidate.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(candidate => candidate.StatusCode == HttpStatusCode.BadRequest).Should().Be(1);
    }

    private async Task<LoginResult> LoginAsync(Guid deviceId)
    {
        var email = $"user-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!";

        var register = await _identityApi.Register(email, password);
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await _identityApi.PasswordLogin(email, password, deviceId);
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        tokens.Should().NotBeNull();
        tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();

        return new LoginResult(login, tokens.AccessToken!, tokens.RefreshToken!);
    }

    private async Task<LoginResult> LoginAsync(Guid deviceId, string email, string password)
    {
        var login = await _identityApi.PasswordLogin(email, password, deviceId);
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        tokens.Should().NotBeNull();
        tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();

        return new LoginResult(login, tokens.AccessToken!, tokens.RefreshToken!);
    }

    private async Task RegisterAsync(string email, string password)
    {
        var register = await _identityApi.Register(email, password);
        register.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private sealed record LoginResult(
        HttpResponseMessage Response,
        string AccessToken,
        string RefreshToken);

    private sealed record TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }
    }

    private sealed record OAuthErrorResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
