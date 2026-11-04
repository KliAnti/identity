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
builder.Services
    .AddGrpcClient<global::Identity.Grpc.Identity.IdentityClient>(options =>
    {
        var address = builder.Configuration["Grpc:IdentityAddress"]
            ?? throw new InvalidOperationException("Configuration value 'Grpc:IdentityAddress' is required.");
        options.Address = new Uri(address);
    })
    .AddInterceptor<TraceIdInterceptor>();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("postgresql");
builder.Services.AddScoped<IdentityService>();

var app = builder.Build();

app.MapGrpcService<IdentityService>();
app.MapGrpcHealthChecksService();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program
{
}
