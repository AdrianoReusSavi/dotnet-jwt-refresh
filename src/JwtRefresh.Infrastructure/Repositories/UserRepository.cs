using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Domain.Auth;
using JwtRefresh.Infrastructure.Context;

namespace JwtRefresh.Infrastructure.Repositories;

public class UserRepository(DataContext context) : IUserRepository
{
    private readonly DataContext _context = context;

    public void Add(User user)
        => throw new NotImplementedException();

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}