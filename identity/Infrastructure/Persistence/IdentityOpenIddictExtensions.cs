using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Регистрирует и инициализирует сервер OpenIddict для Identity.
/// </summary>
public static class IdentityOpenIddictExtensions
{
    /// <summary>
    /// Добавляет настройки выдачи токенов, подписи и требования к защищённому транспорту.
    /// </summary>
    /// <param name="services">Коллекция зависимостей приложения.</param>
    /// <param name="configuration">Конфигурация; должна содержать абсолютный <c>OpenIddict:Issuer</c>.</param>
    /// <param name="environment">Среда хостинга; только Development и Testing допускают временный сертификат и HTTP.</param>
    /// <returns>Та же коллекция сервисов для продолжения регистрации.</returns>
    /// <exception cref="InvalidOperationException">Issuer отсутствует или некорректен, либо в рабочей среде не задан сертификат.</exception>
    public static IServiceCollection AddIdentityOpenIddict(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var issuerValue = configuration["OpenIddict:Issuer"]
            ?? throw new InvalidOperationException("OpenIddict issuer must be configured.");
        if (!Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer))
        {
            throw new InvalidOperationException("OpenIddict issuer must be an absolute URI.");
        }

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
                options.SetIssuer(issuer)
                    .SetTokenEndpointUris("/connect/token")
                    .AllowPasswordFlow()
                    .RegisterScopes(IdentityOpenIddictDefaults.ApiScope)
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

                if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                {
                    aspNetCoreOptions.DisableTransportSecurityRequirement();
                }
            });

        return services;
    }

    /// <summary>
    /// Идемпотентно создаёт в OpenIddict публичное приложение и API scope, если они ещё не существуют.
    /// </summary>
    /// <param name="services">Корневой провайдер зависимостей с зарегистрированными хранилищами OpenIddict.</param>
    /// <returns>Асинхронная операция инициализации записей.</returns>
    /// <remarks>База и миграции должны быть подготовлены до вызова метода.</remarks>
    public static async Task InitializeOpenIddictClientAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        if (await applicationManager.FindByClientIdAsync(IdentityOpenIddictDefaults.NativeClientId) is null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
            ClientId = IdentityOpenIddictDefaults.NativeClientId,
                ClientType = OpenIddictConstants.ClientTypes.Public,
                DisplayName = "VA native client",
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.Password,
                    OpenIddictConstants.Permissions.Prefixes.Scope + IdentityOpenIddictDefaults.ApiScope
                }
            });
        }

        if (await scopeManager.FindByNameAsync(IdentityOpenIddictDefaults.ApiScope) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
            Name = IdentityOpenIddictDefaults.ApiScope,
                DisplayName = "VA API",
            Resources = { IdentityOpenIddictDefaults.ApiAudience }
            });
        }
    }
}