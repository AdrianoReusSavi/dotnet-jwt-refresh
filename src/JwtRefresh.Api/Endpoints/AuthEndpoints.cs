using System.Diagnostics;
using JwtRefresh.Api.Endpoints.Contracts;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Interfaces.Security;
using JwtRefresh.Application.Interfaces.Service;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JwtRefresh.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", RegisterAsync);

        group.MapPost("/login", LoginAsync);

        return app;
    }

    private static async Task<Results<Created<RegisterResponse>, Conflict>> RegisterAsync(
        RegisterRequest request, IUserService userService, CancellationToken cancellationToken)
    {
        var result = await userService.RegisterAsync(request.Email, request.Password, cancellationToken);

        return result.Status switch
        {
            RegisterStatus.Registered => TypedResults.Created((string?)null, new RegisterResponse(result.UserId!.Value)),
            RegisterStatus.EmailTaken => TypedResults.Conflict(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        IUserService userService,
        IRefreshTokenService refreshTokenService,
        IAccessTokenIssuer accessTokenIssuer,
        CancellationToken cancellationToken)
    {
        var result = await userService.LoginAsync(request.Email, request.Password, cancellationToken);

        if (result.Status is not LoginStatus.Succeeded)
            return TypedResults.Unauthorized();

        var userId = result.UserId!.Value;
        var refreshToken = await refreshTokenService.IssueAsync(userId, cancellationToken);
        var accessToken = accessTokenIssuer.Issue(userId);

        return TypedResults.Ok(new LoginResponse(accessToken.Value, refreshToken, accessToken.ExpiresAt));
    }
}