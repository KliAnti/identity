using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Identity.Infrastructure.Persistence.Models.User;

namespace Identity.Features.Authentication.Registration;

public static class RegistrationEndpoint
{
    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    public static IEndpointRouteBuilder MapRegistrationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/register", RegisterAsync);
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegistrationRequest request,
        UserManager<UserModel> userManager,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email)
            || email.Length > 256
            || !EmailAddressValidator.IsValid(email))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Email)] = ["A valid email address is required."]
            });
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Password)] = ["A password between 1 and 1024 characters is required."]
            });
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return Results.Conflict(new { error = "email_already_registered" });
        }

        var user = new UserModel
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = userManager.NormalizeEmail(email) ?? email.ToUpperInvariant(),
            EmailConfirmed = true // TODO: Implement email confirmation flow in the future
        };

        try
        {
            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return Results.ValidationProblem(result.Errors
                    .GroupBy(error => error.Code, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.Description).ToArray(),
                        StringComparer.Ordinal));
            }
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            return Results.Conflict(new { error = "email_already_registered" });
        }

        return Results.Json(
            new RegistrationResponse(user.Id, user.Email),
            statusCode: StatusCodes.Status201Created);
    }
}