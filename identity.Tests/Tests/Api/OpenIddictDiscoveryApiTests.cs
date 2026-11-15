using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Identity.Tests.Tests.Api;

[Collection(IdentityApiCollection.Name)]
public sealed class OpenIddictDiscoveryApiTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task Discovery_AdvertisesConfiguredIssuerEndpointGrantAndScope()
    {
        using var response = await fixture.CreateClient().GetAsync("/.well-known/openid-configuration");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metadata = document.RootElement;

        metadata.GetProperty("issuer").GetString().Should().Be("http://identity/");
        metadata.GetProperty("token_endpoint").GetString().Should().Be("http://localhost/connect/token");
        metadata.GetProperty("grant_types_supported").EnumerateArray()
            .Select(value => value.GetString())
            .Should().Contain("password");
        metadata.GetProperty("scopes_supported").EnumerateArray()
            .Select(value => value.GetString())
            .Should().Contain("api");
    }
}