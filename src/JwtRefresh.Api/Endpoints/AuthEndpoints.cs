using System.Diagnostics;
using System.Security.Claims;
using JwtRefresh.Api.Endpoints.Contracts;
using JwtRefresh.Api.RateLimiting;
using JwtRefresh.Application.Enums;
using JwtRefresh.Application.Interfaces.Security;
using JwtRefresh.Application.Interfaces.Service;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.JsonWebTokens;

namespace JwtRefresh.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", RegisterAsync)
            .RequireRateLimiting(RateLimitPolicies.Credentials);

        group.MapPost("/login", LoginAsync)
            .RequireRateLimiting(RateLimitPolicies.Credentials);

        group.MapGet("/me", Me).RequireAuthorization();

        group.MapPost("/refresh", RefreshAsync);

        group.MapPost("/logout", LogoutAsync);

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

    private static async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult>> LoginAsync(
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

        return TypedResults.Ok(new TokenResponse(accessToken.Value, refreshToken, accessToken.ExpiresAt));
    }

    private static Ok<MeResponse> Me(ClaimsPrincipal user)
    {
        var userId = Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        return TypedResults.Ok(new MeResponse(userId));
    }

    private static async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult, Conflict>> RefreshAsync(
        RefreshRequest request,
        IRefreshTokenService refreshTokenService,
        IAccessTokenIssuer accessTokenIssuer,
        CancellationToken cancellationToken)
    {
        var result = await refreshTokenService.RefreshAsync(request.RefreshToken, cancellationToken);

        if (result.Status is RefreshStatus.Refreshed)
        {
            var accessToken = accessTokenIssuer.Issue(result.UserId!.Value);

            return TypedResults.Ok(new TokenResponse(accessToken.Value, result.RawValue!, accessToken.ExpiresAt));
        }

        return result.Status switch
        {
            RefreshStatus.NotFound or
            RefreshStatus.Expired or
            RefreshStatus.Revoked or
            RefreshStatus.ReuseDetected => TypedResults.Unauthorized(),
            RefreshStatus.Conflict => TypedResults.Conflict(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<NoContent> LogoutAsync(
        LogoutRequest request,
        IRefreshTokenService refreshTokenService,
        CancellationToken cancellationToken)
    {
        await refreshTokenService.RevokeAsync(request.RefreshToken, cancellationToken);

        return TypedResults.NoContent();
    }
}