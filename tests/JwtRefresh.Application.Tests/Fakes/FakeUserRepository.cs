using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Domain.Auth;

namespace JwtRefresh.Application.Tests.Fakes;

public class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public void Add(User user)
        => Users.Add(user);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.FirstOrDefault(f => f.Email == User.NormalizeEmail(email)));
}