# dotnet-jwt-refresh

JWT refresh token rotation with reuse detection — .NET 10, ASP.NET Core, PostgreSQL, Docker, TDD.

> **Status: work in progress.** The domain model and the persistence layer (EF Core + PostgreSQL) are complete and tested. Concurrent refresh handling and family revocation are the next steps (see [Roadmap](#roadmap)).

## How it works

- **Access tokens** are short-lived JWTs and are never stored.
- **Refresh tokens** are random 256-bit values. The client gets the raw value; the database keeps only its SHA-256 hash.
- **Every refresh rotates the token:** the current one is marked as used and a new one is issued in the same *family* (the chain of tokens that started at one login).
- **Reuse detection:** a token that was already used and shows up again means someone else has a copy. The rotation is refused with `ReuseDetected`, so the whole family can be revoked.
- **Two deadlines:** each token expires after a period of inactivity, and the whole session has an absolute limit since login that rotations cannot extend.

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

## Project structure

```text
src/
  JwtRefresh.Domain/                  RefreshToken, PasswordHasher (no framework dependencies)
  JwtRefresh.Application/             repository and unit of work interfaces
  JwtRefresh.Infrastructure/          EF Core mapping, migrations and repositories (PostgreSQL)
  JwtRefresh.Api/                     ASP.NET Core API (in progress)
tests/
  JwtRefresh.Domain.Tests/            unit tests (xUnit)
  JwtRefresh.Infrastructure.Tests/    repository tests against PostgreSQL (Testcontainers)
  JwtRefresh.Api.IntegrationTests/    integration tests (in progress)
docs/adr/                             Architecture Decision Records
```

## Running the tests

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running. The infrastructure tests start a disposable PostgreSQL container with [Testcontainers](https://dotnet.testcontainers.org/).

```bash
dotnet test
```

The domain was built test-first: every behavior went through a failing test before the implementation, and the commit history keeps each RED → GREEN → REFACTOR step.

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
- [ ] Concurrent refresh handling (two requests with the same token)
- [ ] Family revocation on reuse detection
- [ ] Endpoints: register, login, refresh, logout
- [ ] Integration tests with `WebApplicationFactory` and Testcontainers
- [ ] ProblemDetails, structured logging and rate limiting

## License

[MIT](LICENSE)
