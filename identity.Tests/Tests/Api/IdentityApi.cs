using System.Net.Http.Headers;
using Identity.Tests.Tests.Routes;
using TestFramework.Http;

namespace Identity.Tests.Tests.Api;

/// <summary>Высокоуровневые операции Identity HTTP API.</summary>
public sealed class IdentityApi
{
    private readonly ApiClient _client;

    public IdentityApi(ApiClient client)
    {
        _client = client;
    }

    public Task<HttpResponseMessage> Register(string email, string password)
    {
        return _client.Post(IdentityRoutes.Register, new { email, password });
    }

    public Task<HttpResponseMessage> PasswordLogin(string email, string password, Guid deviceId)
    {
        return _client.PostForm(IdentityRoutes.Token,
        [
            new("grant_type", "password"),
            new("username", email),
            new("password", password),
            new("scope", "api offline_access"),
            new("device_id", deviceId.ToString()),
            new("client_id", "va-native")
        ]);
    }

    public Task<HttpResponseMessage> Refresh(string refreshToken)
    {
        return _client.PostForm(IdentityRoutes.Token,
        [
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken),
            new("scope", "api offline_access"),
            new("client_id", "va-native")
        ]);
    }

    public Task<HttpResponseMessage> Logout(string accessToken)
    {
        return PostAuthenticated(IdentityRoutes.Logout, accessToken);
    }

    public Task<HttpResponseMessage> LogoutAll(string accessToken)
    {
        return PostAuthenticated(IdentityRoutes.LogoutAll, accessToken);
    }

    private Task<HttpResponseMessage> PostAuthenticated(string url, string accessToken)
    {
        return _client.PostFormBearer(url, accessToken, []);
    }
}
