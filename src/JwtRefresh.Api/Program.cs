using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using JwtRefresh.Api.Endpoints;
using JwtRefresh.Api.RateLimiting;
using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Application.Interfaces.Security;
using JwtRefresh.Application.Interfaces.Service;
using JwtRefresh.Application.Services;
using JwtRefresh.Infrastructure.Auth;
using JwtRefresh.Infrastructure.Context;
using JwtRefresh.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<DataContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

builder.Services.AddOptions<RefreshTokenOptions>()
    .BindConfiguration(RefreshTokenOptions.SectionName)
    .Validate(v => v.Lifetime > TimeSpan.Zero && v.SessionLifetime > v.Lifetime,
        "RefreshToken: Lifetime must be positive and shorter than SessionLifetime.")
    .ValidateOnStart();

builder.Services.AddOptions<AccessTokenOptions>()
    .BindConfiguration(AccessTokenOptions.SectionName)
    .Validate(v => !string.IsNullOrWhiteSpace(v.Issuer)
            && !string.IsNullOrWhiteSpace(v.Audience)
            && Encoding.UTF8.GetByteCount(v.SigningKey) >= 32
            && v.Lifetime > TimeSpan.Zero,
        "AccessToken: Issuer and Audience must not be empty, SigningKey must be at least 32 bytes and Lifetime must be positive.")
    .ValidateOnStart();

builder.Services.AddRateLimiter(_ => { });

builder.Services.AddOptions<RateLimitingOptions>()
    .BindConfiguration(RateLimitingOptions.SectionName)
    .Validate(v => v.PermitLimit > 0 && v.Window > TimeSpan.Zero,
        "RateLimiting: PermitLimit and Window must be positive.")
    .ValidateOnStart();

builder.Services.AddOptions<RateLimiterOptions>()
    .Configure<IOptions<RateLimitingOptions>>((limiter, rateLimiting) =>
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        limiter.AddPolicy(RateLimitPolicies.Credentials, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimiting.Value.PermitLimit,
                    Window = rateLimiting.Value.Window,
                    AutoReplenishment = true,
                    QueueLimit = 0
                }));

        limiter.OnRejected = (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
            }

            return ValueTask.CompletedTask;
        };
    });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AccessTokenOptions>>((bearer, accessToken) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = accessToken.Value.Issuer,
            ValidAudience = accessToken.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accessToken.Value.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddValidation();

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapAuthEndpoints();

app.Run();