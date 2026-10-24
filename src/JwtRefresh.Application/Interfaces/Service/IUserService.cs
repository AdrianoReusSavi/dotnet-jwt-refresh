using JwtRefresh.Application.Auth;

namespace JwtRefresh.Application.Interfaces.Service;

public interface IUserService
{
    Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}