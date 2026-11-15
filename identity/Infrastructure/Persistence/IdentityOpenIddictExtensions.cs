using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Identity.Infrastructure.Persistence;

public static class IdentityOpenIddictExtensions
{
    private const string ApiScope = "api";
    private const string ApiAudience = "va-api";
    private const string NativeClientId = "va-native";

    public static IServiceCollection AddIdentityOpenIddict(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var issuer = configuration["OpenIddict:Issuer"] ?? "http://identity/";
        var signingCertificatePath = configuration["OpenIddict:SigningCertificate:Path"];
        var signingCertificatePassword = configuration["OpenIddict:SigningCertificate:Password"];
        var signingCertificatePasswordFile = configuration["OpenIddict:SigningCertificate:PasswordFile"];

        if (!string.IsNullOrWhiteSpace(signingCertificatePasswordFile))
        {
            if (!string.IsNullOrEmpty(signingCertificatePassword))
            {
                throw new InvalidOperationException(
                    "Configure either the signing certificate password or its file, not both.");
            }

            signingCertificatePassword = File.ReadAllText(signingCertificatePasswordFile)
                .TrimEnd('\r', '\n');
        }

        if (string.IsNullOrWhiteSpace(signingCertificatePath)
            && !environment.IsDevelopment()
            && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "OpenIddict signing certificate path must be configured outside Development.");
        }

        services.AddOpenIddict()
            .AddServer(options =>
            {
                options.SetIssuer(new Uri(issuer))
                    .SetTokenEndpointUris("/connect/token")
                    .AllowPasswordFlow()
                    .RegisterScopes(ApiScope)
                    .SetAccessTokenLifetime(TimeSpan.FromMinutes(10))
                    .AddEphemeralEncryptionKey()
                    .DisableAccessTokenEncryption();

                if (string.IsNullOrWhiteSpace(signingCertificatePath))
                {
                    options.AddDevelopmentSigningCertificate();
                }
                else
                {
                    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                        signingCertificatePath,
                        signingCertificatePassword);
                    options.AddSigningCertificate(certificate);
                }

                var aspNetCoreOptions = options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough();

                if (environment.IsDevelopment())
                {
                    aspNetCoreOptions.DisableTransportSecurityRequirement();
                }
            });

        return services;
    }

    public static async Task InitializeOpenIddictClientAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        if (await applicationManager.FindByClientIdAsync(NativeClientId) is null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = NativeClientId,
                ClientType = OpenIddictConstants.ClientTypes.Public,
                DisplayName = "VA native client",
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.Password,
                    OpenIddictConstants.Permissions.Prefixes.Scope + ApiScope
                }
            });
        }

        if (await scopeManager.FindByNameAsync(ApiScope) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = ApiScope,
                DisplayName = "VA API",
                Resources = { ApiAudience }
            });
        }
    }
}