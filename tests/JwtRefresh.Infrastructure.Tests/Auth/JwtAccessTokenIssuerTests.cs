using System.Text;
using JwtRefresh.Application.Auth;
using JwtRefresh.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JwtRefresh.Infrastructure.Tests.Auth;

public class JwtAccessTokenIssuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly JwtAccessTokenIssuer _issuer;

    private const string Issuer = "jwt-refresh-tests-issuer";
    private const string Audience = "jwt-refresh-tests-audience";
    private const string SigningKey = "jwt-refresh-tests-signing-key-0123";
    private const string OtherSigningKey = "jwt-refresh-tests-signing-key-4567";

    public JwtAccessTokenIssuerTests()
    {
        var options = Options.Create(new AccessTokenOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = SigningKey,
            Lifetime = Lifetime,
        });

        _issuer = new JwtAccessTokenIssuer(_timeProvider, options);
    }

    private static TokenValidationParameters ValidationParameters(string signingKey)
        => new()
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            // The token was issued with the fake clock (January 2026), so against the real clock it looks expired.
            // Expiration is checked in its own test by reading the claims.
            ValidateLifetime = false,
        };

    private static JsonWebToken ReadToken(AccessToken token)
        => new JsonWebTokenHandler().ReadJsonWebToken(token.Value);

    [Fact]
    public async Task Issue_Signs_The_Token_With_The_Signing_Key()
    {
        var token = _issuer.Issue(Guid.NewGuid());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, ValidationParameters(SigningKey));

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task Issue_Token_Fails_Validation_With_Another_Key()
    {
        var token = _issuer.Issue(Guid.NewGuid());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, ValidationParameters(OtherSigningKey));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Issue_Sets_Subject_Issuer_And_Audience()
    {
        var userId = Guid.NewGuid();

        var token = ReadToken(_issuer.Issue(userId));

        Assert.Equal(userId.ToString(), token.Subject);
        Assert.Equal(Issuer, token.Issuer);
        Assert.Equal(Audience, Assert.Single(token.Audiences));
    }

    [Fact]
    public void Issue_Sets_IssuedAt_And_Expiration_From_The_Time_Provider()
    {
        var accessToken = _issuer.Issue(Guid.NewGuid());

        var token = ReadToken(accessToken);

        Assert.Equal(Now.UtcDateTime, token.IssuedAt);
        Assert.Equal((Now + Lifetime).UtcDateTime, token.ValidTo);
        Assert.Equal(Now + Lifetime, accessToken.ExpiresAt);
    }

    [Fact]
    public void Issue_Gives_Each_Token_A_Unique_Id()
    {
        var userId = Guid.NewGuid();

        var first = ReadToken(_issuer.Issue(userId));
        var second = ReadToken(_issuer.Issue(userId));

        Assert.False(string.IsNullOrWhiteSpace(first.Id));
        Assert.NotEqual(first.Id, second.Id);
    }
}