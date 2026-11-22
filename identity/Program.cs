using Serilog;
using Serilog.Enrichers.Span;
using identity.Grpc;
using Identity.Infrastructure.Persistence;
using Identity.Services;
using Identity.Features.Authentication;
using Identity.Features.Authentication.LoginAttemptRateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, loggerConfiguration) =>
    loggerConfiguration
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .WriteTo.Console(
            outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddGrpc(options =>
    options.Interceptors.Add<TraceIdInterceptor>());
builder.Services.AddIdentityPersistence(builder.Configuration);
builder.Services.AddIdentityOpenIddict(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<LoginAttemptRateLimiter>();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("postgresql")
    .AddCheck("identity", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
builder.Services.AddScoped<IdentityService>();
builder.Services.AddScoped<SessionRevocationService>();
builder.Services.AddScoped<RefreshTokenReplayHandler>();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await dbContext.Database.MigrateAsync();

    if (builder.Configuration.GetValue("OpenIddict:InitializeClient", true))
    {
        await app.Services.InitializeOpenIddictClientAsync();
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<IdentityService>();
app.MapGrpcHealthChecksService();
app.MapPasswordTokenEndpoint();

app.Run();
