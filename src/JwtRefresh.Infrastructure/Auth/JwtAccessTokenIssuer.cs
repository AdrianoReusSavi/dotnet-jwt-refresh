using System.Text;
using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Interfaces.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JwtRefresh.Infrastructure.Auth;

public class JwtAccessTokenIssuer(
        TimeProvider timeProvider,
        IOptions<AccessTokenOptions> options
    ) : IAccessTokenIssuer
{
    public AccessToken Issue(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = timeProvider.GetUtcNow();
        var expiresAt = now + options.Value.Lifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Value.Issuer,
            Audience = options.Value.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = credentials,
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}