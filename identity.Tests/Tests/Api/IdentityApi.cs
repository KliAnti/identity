using Grpc.Core;
using identity.Grpc;
using Identity.Tests.Tests.Routes;
using TestFramework.Http;

namespace Identity.Tests.Tests.Api;

/// <summary>Высокоуровневые операции Identity HTTP API.</summary>
public sealed class IdentityApi
{
    private readonly ApiClient _client;
    private readonly global::identity.Grpc.Identity.IdentityClient _grpcClient;

    public IdentityApi(ApiClient client, global::identity.Grpc.Identity.IdentityClient grpcClient)
    {
        _client = client;
        _grpcClient = grpcClient;
    }

    public Task<RegisterResponse> Register(string email, string password)
    {
        return _grpcClient.RegisterAsync(new RegisterRequest { Email = email, Password = password }).ResponseAsync;
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

    public Task<LogoutResponse> Logout(string accessToken)
    {
        return _grpcClient.LogoutAsync(new LogoutRequest(), CreateAuthorizationMetadata(accessToken)).ResponseAsync;
    }

    public Task<LogoutResponse> LogoutAll(string accessToken)
    {
        return _grpcClient.LogoutAllAsync(new LogoutRequest(), CreateAuthorizationMetadata(accessToken)).ResponseAsync;
    }

    private static Metadata CreateAuthorizationMetadata(string accessToken)
    {
        return new Metadata { { "Authorization", $"Bearer {accessToken}" } };
    }
}
