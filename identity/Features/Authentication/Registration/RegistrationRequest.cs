namespace Identity.Features.Authentication.Registration;

public sealed record RegistrationRequest(string? Email, string? Password);