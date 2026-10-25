using System.Text;
using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Application.Interfaces.Security;
using JwtRefresh.Application.Interfaces.Service;
using JwtRefresh.Application.Services;
using JwtRefresh.Infrastructure.Auth;
using JwtRefresh.Infrastructure.Context;
using JwtRefresh.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<DataContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();