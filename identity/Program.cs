using Serilog;
using Serilog.Enrichers.Span;
using Identity.Grpc;
using Identity.Infrastructure.Persistence;
using Identity.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

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
builder.Services.AddGrpcHealthChecks();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("postgresql")
    .AddCheck("identity", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
builder.Services.AddScoped<IdentityService>();

var app = builder.Build();

app.MapGrpcService<IdentityService>();
app.MapGrpcHealthChecksService();

app.Run();
