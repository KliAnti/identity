using Serilog;
using Serilog.Enrichers.Span;
using va_identity.Grpc;
using va_identity.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, loggerConfiguration) =>
    loggerConfiguration
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
        .WriteTo.Console(
            outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddGrpc(options =>
    options.Interceptors.Add<TraceIdInterceptor>());
builder.Services
    .AddGrpcClient<Identity.IdentityClient>(options =>
    {
        var address = builder.Configuration["Grpc:IdentityAddress"]
            ?? throw new InvalidOperationException("Configuration value 'Grpc:IdentityAddress' is required.");
        options.Address = new Uri(address);
    })
    .AddInterceptor<TraceIdInterceptor>();
builder.Services.AddGrpcHealthChecks();
builder.Services.AddHealthChecks()
    .AddCheck("identity", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
builder.Services.AddScoped<IdentityService>();

var app = builder.Build();

app.MapGrpcService<IdentityService>();
app.MapGrpcHealthChecksService();

app.Run();
