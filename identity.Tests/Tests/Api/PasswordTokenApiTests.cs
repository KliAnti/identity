using System.Text.Json;
using FluentAssertions;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Identity.Tests.Tests.Api;

[Collection(IdentityApiCollection.Name)]
public sealed class PasswordTokenApiTests(IdentityApiFixture fixture)
{
    private const string ValidPassword = "Valid-password123!";

    [Fact]
    public async Task Token_WhenCredentialsAreValid_ReturnsSignedAccessTokenWithClaims()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var deviceId = Guid.NewGuid();
        var user = await fixture.CreateUserAsync(email, ValidPassword, emailConfirmed: true, "admin");

        using var client = fixture.CreateClient();
        using var response = await RequestTokenAsync(client, email, ValidPassword, deviceId);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var accessToken = responseJson.RootElement.GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();
        var tokenParts = accessToken!.Split('.');
        tokenParts.Should().HaveCount(3);
        tokenParts[2].Should().NotBeNullOrWhiteSpace();

        using var payload = JsonDocument.Parse(DecodeBase64Url(tokenParts[1]));
        var claims = payload.RootElement;
        claims.GetProperty("sub").GetString().Should().Be(user.Id.ToString());
        claims.GetProperty("jti").GetString().Should().NotBeNullOrWhiteSpace();
        claims.GetProperty("iss").GetString().Should().Be("http://identity/");
        GetStringValues(claims.GetProperty("aud")).Should().Contain("va-api");
        GetUnixTime(claims, "iat").Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        GetUnixTime(claims, "exp").Should().BeAfter(DateTimeOffset.UtcNow)
            .And.BeOnOrBefore(DateTimeOffset.UtcNow.AddMinutes(10));
        GetStringValues(claims.GetProperty("role")).Should().Contain("admin");
        claims.GetProperty("device_id").GetString().Should().Be(deviceId.ToString());
        claims.TryGetProperty("password", out _).Should().BeFalse();
        claims.TryGetProperty("PasswordHash", out _).Should().BeFalse();
        claims.TryGetProperty("PasswordSalt", out _).Should().BeFalse();

        var keySet = new JsonWebKeySet(await client.GetStringAsync("/.well-known/jwks"));
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(
            accessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "http://identity/",
                ValidateAudience = true,
                ValidAudience = "va-api",
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keySet.GetSigningKeys(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(1)
            });
        validation.IsValid.Should().BeTrue(validation.Exception?.ToString());

        var sessionId = Guid.Parse(claims.GetProperty("sid").GetString()!);
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var device = await dbContext.Devices.SingleAsync(candidate => candidate.Id == deviceId);
        var session = await dbContext.Sessions.SingleAsync(candidate => candidate.Id == sessionId);
        device.UserId.Should().Be(user.Id);
        device.RevokedAt.Should().BeNull();
        session.DeviceId.Should().Be(device.Id);
        session.RevokedAt.Should().BeNull();
        session.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        GetUnixTime(claims, "exp").Should().BeCloseTo(session.ExpiresAt, TimeSpan.FromSeconds(1));

        using var subsequentResponse = await RequestTokenAsync(client, email, ValidPassword, deviceId);
        subsequentResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        using var subsequentJson = JsonDocument.Parse(await subsequentResponse.Content.ReadAsStringAsync());
        using var subsequentPayload = JsonDocument.Parse(
            DecodeBase64Url(subsequentJson.RootElement.GetProperty("access_token").GetString()!.Split('.')[1]));
        var subsequentSessionId = Guid.Parse(subsequentPayload.RootElement.GetProperty("sid").GetString()!);
        subsequentSessionId.Should().NotBe(sessionId);
        (await dbContext.Sessions.CountAsync(candidate => candidate.DeviceId == deviceId)).Should().Be(2);
    }

    [Fact]
    public async Task Token_WhenDeviceBelongsToAnotherUserOrIsRevoked_ReturnsInvalidGrant()
    {
        var ownerEmail = $"{Guid.NewGuid():N}@example.test";
        var otherEmail = $"{Guid.NewGuid():N}@example.test";
        var deviceId = Guid.NewGuid();
        await fixture.CreateUserAsync(ownerEmail, ValidPassword, emailConfirmed: true);
        await fixture.CreateUserAsync(otherEmail, ValidPassword, emailConfirmed: true);

        using var ownerResponse = await RequestTokenAsync(ownerEmail, ValidPassword, deviceId);
        ownerResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        using var otherUserResponse = await RequestTokenAsync(otherEmail, ValidPassword, deviceId);
        otherUserResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadOAuthErrorAsync(otherUserResponse)).Error.Should().Be("invalid_grant");

        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var device = await dbContext.Devices.SingleAsync(candidate => candidate.Id == deviceId);
        device.RevokedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

        using var revokedDeviceResponse = await RequestTokenAsync(ownerEmail, ValidPassword, deviceId);
        revokedDeviceResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await ReadOAuthErrorAsync(revokedDeviceResponse)).Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Token_WhenPasswordIsWrongOrEmailIsUnconfirmed_ReturnsSameError()
    {
        var wrongPasswordEmail = $"{Guid.NewGuid():N}@example.test";
        var unconfirmedEmail = $"{Guid.NewGuid():N}@example.test";
        var missingEmail = $"{Guid.NewGuid():N}@example.test";
        await fixture.CreateUserAsync(wrongPasswordEmail, ValidPassword, emailConfirmed: true);
        await fixture.CreateUserAsync(unconfirmedEmail, ValidPassword, emailConfirmed: false);

        using var wrongPasswordResponse = await RequestTokenAsync(wrongPasswordEmail, "Wrong-password123!");
        using var unconfirmedResponse = await RequestTokenAsync(unconfirmedEmail, ValidPassword);
        using var missingUserResponse = await RequestTokenAsync(missingEmail, ValidPassword);

        wrongPasswordResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        unconfirmedResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        missingUserResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var wrongPasswordError = await ReadOAuthErrorAsync(wrongPasswordResponse);
        var unconfirmedError = await ReadOAuthErrorAsync(unconfirmedResponse);
        var missingUserError = await ReadOAuthErrorAsync(missingUserResponse);
        wrongPasswordError.Should().Be(unconfirmedError);
        wrongPasswordError.Should().Be(missingUserError);
        wrongPasswordError.Error.Should().Be("invalid_grant");
        wrongPasswordError.Description.Should().Be("The email or password is invalid.");
    }

    [Fact]
    public async Task Token_WhenLoginLimitIsExceeded_ReturnsTooManyRequests()
    {
        var email = $"{Guid.NewGuid():N}@example.test";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await RequestTokenAsync(email, "Wrong-password123!");
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        }

        using var limitedResponse = await RequestTokenAsync(email, "Wrong-password123!");
        limitedResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.TooManyRequests);
        limitedResponse.Headers.RetryAfter?.Delta.Should().BeGreaterThan(TimeSpan.Zero)
            .And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(1));
    }

    private async Task<HttpResponseMessage> RequestTokenAsync(
        string email,
        string password,
        Guid? deviceId = null)
    {
        using var client = fixture.CreateClient();
        return await RequestTokenAsync(client, email, password, deviceId);
    }

    private static async Task<HttpResponseMessage> RequestTokenAsync(
        HttpClient client,
        string email,
        string password,
        Guid? deviceId = null)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "va-native",
            ["username"] = email,
            ["password"] = password,
            ["scope"] = "api",
            ["device_id"] = (deviceId ?? Guid.NewGuid()).ToString()
        });

        return await client.PostAsync("/connect/token", content);
    }

    private static async Task<(string Error, string Description)> ReadOAuthErrorAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            document.RootElement.GetProperty("error").GetString()!,
            document.RootElement.GetProperty("error_description").GetString()!);
    }

    private static IEnumerable<string?> GetStringValues(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().Select(value => value.GetString())
            : [element.GetString()];

    private static DateTimeOffset GetUnixTime(JsonElement claims, string name) =>
        DateTimeOffset.FromUnixTimeSeconds(claims.GetProperty(name).GetInt64());

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}