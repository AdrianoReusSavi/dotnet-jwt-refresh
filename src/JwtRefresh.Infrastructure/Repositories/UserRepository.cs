using JwtRefresh.Application.Interfaces.Repository;
using JwtRefresh.Domain.Auth;
using JwtRefresh.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace JwtRefresh.Infrastructure.Repositories;

public class UserRepository(DataContext context) : IUserRepository
{
    private readonly DataContext _context = context;

    public void Add(User user)
        => _context.Users.Add(user);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = User.NormalizeEmail(email);

        return await _context.Users
            .FirstOrDefaultAsync(f => f.Email == normalizedEmail, cancellationToken);
    }
}