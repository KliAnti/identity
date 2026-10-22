using Serilog;
using va_identity.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, loggerConfiguration) =>
    loggerConfiguration.WriteTo.Console());

builder.Services.AddGrpc();
builder.Services.AddScoped<IdentityService>();

var app = builder.Build();

app.MapGrpcService<IdentityService>();

app.Run();
