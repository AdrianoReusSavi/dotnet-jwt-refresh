namespace JwtRefresh.Domain.Auth;

public sealed class User
{
    public Guid Id { get; }
    public string Email { get; }
    public string PasswordHash { get; }
    public DateTimeOffset CreatedAt { get; }

    // Used by EF Core when loading from the database; properties are filled from the columns.
    private User()
    {
        Email = null!;
        PasswordHash = null!;
    }

    private User(Guid id, string email, string passwordHash, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    public static User Register(string email, string password, DateTimeOffset now)
        => throw new NotImplementedException();

    public static string NormalizeEmail(string email)
        => throw new NotImplementedException();

    public bool VerifyPassword(string password)
        => throw new NotImplementedException();
}