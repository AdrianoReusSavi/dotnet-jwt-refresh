using JwtRefresh.Application.Auth;

namespace JwtRefresh.Application.Interfaces.Security;

public interface IAccessTokenIssuer
{
    AccessToken Issue(Guid userId);
}