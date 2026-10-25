# dotnet-jwt-refresh

JWT refresh token rotation with reuse detection — .NET 10, ASP.NET Core, PostgreSQL, Docker, TDD.

> **Status: work in progress.** The domain model, the persistence layer (EF Core + PostgreSQL) and the application services are complete and tested, including concurrent requests: refresh tokens (issue, refresh, logout and family revocation) and users (register and login). The HTTP endpoints are the next step (see [Roadmap](#roadmap)).

## How it works

- **Access tokens** are short-lived JWTs and are never stored.
- **Refresh tokens** are random 256-bit values. The client gets the raw value; the database keeps only its SHA-256 hash.
- **Every refresh rotates the token:** the current one is marked as used and a new one is issued in the same *family* (the chain of tokens that started at one login).
- **Reuse detection:** a token that was already used and shows up again means someone else has a copy. The rotation is refused with `ReuseDetected` and the whole family is revoked, which ends the session for both the attacker and the victim.
- **Two deadlines:** each token expires after a period of inactivity, and the whole session has an absolute limit since login that rotations cannot extend.
- **Logout revokes the family**, so a successor created by a refresh that finished at the same time is revoked too. An unknown token is ignored, so the response never reveals whether a token exists.
- **Users** are a small entity of this project, not ASP.NET Core Identity, so every step of the authentication stays visible in the code. Passwords are hashed with BCrypt. Emails are trimmed and lowercased before they are stored or looked up, and a unique index keeps one account per email.
- **Login does not reveal whether an email exists:** an unknown email and a wrong password get the same answer, and an unknown email still spends the same BCrypt time. Register does reveal it, and returns `EmailTaken`, also when two registers with the same email arrive at the same time (see ADR 5).
- **Concurrent requests:** if two requests change the same token at once, only one commit wins. A losing refresh returns `Conflict` and issues nothing; a losing family revocation reloads the tokens and tries again (up to three attempts), because the family must end up revoked.

| Presented token | Result |
| - | - |
| active | `Rotated` — a new token is issued |
| already used (even if also expired or revoked) | `ReuseDetected` |
| revoked (even if also expired) | `Revoked` |
| expired | `Expired` |

A refused rotation never issues a new token and never changes the stored token.

## Design decisions

The reasoning behind the main choices is recorded as Architecture Decision Records:

1. [SHA-256 for refresh token hashes](docs/adr/0001-sha256-for-refresh-token-hashes.md) — why not BCrypt, and why SHA-256 is safe for tokens but not for passwords.
2. [Separate used and revoked markers, with reuse taking precedence](docs/adr/0002-separate-used-and-revoked-markers.md) — how an attack is told apart from a logout.
3. [Absolute session lifetime](docs/adr/0003-absolute-session-lifetime.md) — why a stolen token cannot keep a session alive forever.
4. [Optimistic concurrency with xmin](docs/adr/0004-optimistic-concurrency-with-xmin.md) — how two requests with the same token are kept from forking a family.
5. [User enumeration on login and register](docs/adr/0005-user-enumeration-on-login-and-register.md) — why login hides whether an email exists and register does not.

## Project structure

```text
src/
  JwtRefresh.Domain/                  RefreshToken, User, PasswordHasher (no framework dependencies)
  JwtRefresh.Application/             RefreshTokenService, UserService, repository and unit of work interfaces
  JwtRefresh.Infrastructure/          EF Core mapping, migrations and repositories (PostgreSQL)
  JwtRefresh.Api/                     ASP.NET Core API (in progress)
tests/
  JwtRefresh.Domain.Tests/            domain unit tests (xUnit)
  JwtRefresh.Application.Tests/       service tests with in-memory fakes and FakeTimeProvider
  JwtRefresh.Infrastructure.Tests/    repository and unit of work tests against PostgreSQL (Testcontainers)
  JwtRefresh.Api.IntegrationTests/    integration tests (in progress)
docs/adr/                             Architecture Decision Records
```

## Running the tests

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running. The infrastructure tests start a disposable PostgreSQL container with [Testcontainers](https://dotnet.testcontainers.org/).

```bash
dotnet test
```

The domain and the application services were built test-first: every behavior went through a failing test before the implementation, and the commit history keeps each RED → GREEN → REFACTOR step.

## Running locally

Start the database and apply the migrations:

```bash
docker compose up -d postgres
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/JwtRefresh.Infrastructure --startup-project src/JwtRefresh.Api
```

The connection string for development is in `src/JwtRefresh.Api/appsettings.Development.json`.

## Roadmap

- [x] Password hashing (BCrypt)
- [x] Refresh token issuing, hashing, rotation and revocation
- [x] Reuse detection and status precedence
- [x] Absolute session lifetime
- [x] Persistence with EF Core and PostgreSQL
- [x] Concurrent refresh handling (two requests with the same token)
- [x] Family revocation on reuse detection, with retry on concurrent conflicts
- [x] Logout (family revocation)
- [x] Users: register and login, with a unique email and no user enumeration on login
- [ ] Endpoints: register, login, refresh, logout
- [ ] Integration tests with `WebApplicationFactory` and Testcontainers
- [ ] ProblemDetails, structured logging and rate limiting

## License

[MIT](LICENSE)
