using Serilog;
using Serilog.Enrichers.Span;
using Identity.Grpc;
using Identity.Infrastructure.Persistence;
using Identity.Services;
using Identity.Features.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

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
builder.Services.AddSingleton<DummyPasswordVerifier>();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("postgresql")
    .AddCheck("identity", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
builder.Services.AddScoped<IdentityService>();

var app = builder.Build();

if (builder.Configuration.GetValue("OpenIddict:InitializeClient", true))
{
    await app.Services.InitializeOpenIddictClientAsync();
}

app.MapGrpcService<IdentityService>();
app.MapGrpcHealthChecksService();
app.MapPasswordTokenEndpoint();

app.Run();
