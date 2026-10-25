using JwtRefresh.Application.Auth;
using JwtRefresh.Application.Interfaces.Security;
using Microsoft.Extensions.Options;

namespace JwtRefresh.Infrastructure.Auth;

public class JwtAccessTokenIssuer(
        TimeProvider timeProvider,
        IOptions<AccessTokenOptions> options
    ) : IAccessTokenIssuer
{
    public AccessToken Issue(Guid userId)
        => throw new NotImplementedException();
}