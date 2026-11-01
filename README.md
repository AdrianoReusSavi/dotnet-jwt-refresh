# dotnet-jwt-refresh

<p>
  <img alt="CI" src="https://github.com/AdrianoReusSavi/dotnet-jwt-refresh/actions/workflows/ci.yml/badge.svg" />
  <img alt="License" src="https://img.shields.io/badge/license-MIT-green" />
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10.0-purple" />
  <img alt="PostgreSQL" src="https://img.shields.io/badge/PostgreSQL-16-blue" />
</p>

JWT refresh token rotation with reuse detection — .NET 10, ASP.NET Core, PostgreSQL, Docker, TDD.

The authentication flow is complete and tested end to end: register, login, refresh, logout and a protected endpoint, over HTTP, against a real PostgreSQL database, including concurrent requests. Errors follow RFC 9457 (ProblemDetails), login and register are rate limited, and security events are logged as structured JSON.

## How it works

- **Access tokens** are short-lived JWTs (HS256, 15 minutes) and are never stored. They carry only `sub` (the user id), `jti`, `iat`, `exp`, `iss` and `aud`; the email is left out, so a token copied from a log does not reveal who it belongs to.
- **Refresh tokens** are random 256-bit values. The client gets the raw value; the database keeps only its SHA-256 hash.
- **Every refresh rotates the token:** the current one is marked as used and a new one is issued in the same *family* (the chain of tokens that started at one login).
- **Reuse detection:** a token that was already used and shows up again means someone else has a copy. The rotation is refused with `ReuseDetected` and the whole family is revoked, which ends the session for both the attacker and the victim.
- **Two deadlines:** each token expires after a period of inactivity, and the whole session has an absolute limit since login that rotations cannot extend.
- **Logout revokes the family**, so a successor created by a refresh that finished at the same time is revoked too, and a client that sends an older token of the session still ends it. Other sessions of the same user (other logins, other devices) are not touched. An unknown token is ignored, so the response never reveals whether a token exists.
- **Logout does not invalidate the access token.** There is no denylist: an access token stays valid until it expires, at most 15 minutes after logout. This is the usual trade-off of stateless access tokens: checking a denylist on every request would bring back the database lookup that JWTs avoid. The short lifetime is what bounds the risk.
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

## API

All endpoints are under `/auth` and exchange JSON.

| Endpoint | Request | Responses |
| - | - | - |
| `POST /auth/register` | `{ email, password }` | `201` `{ userId }` · `409` email taken · `400` invalid input · `429` rate limited |
| `POST /auth/login` | `{ email, password }` | `200` token pair · `401` invalid credentials · `400` invalid input · `429` rate limited |
| `POST /auth/refresh` | `{ refreshToken }` | `200` new token pair · `401` refused · `409` concurrent refresh · `400` invalid input |
| `POST /auth/logout` | `{ refreshToken }` | `204` always · `400` invalid input |
| `GET /auth/me` | `Authorization: Bearer <accessToken>` | `200` `{ userId }` · `401` missing, invalid or expired token |

Login and refresh return the same token pair:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "q7Jx3k...",
  "expiresAt": "2026-10-28T14:15:00+00:00"
}
```

`expiresAt` is when the **access token** expires. It tells the client when to call `/auth/refresh`; the refresh token's own deadlines stay on the server.

Every error is returned as `application/problem+json` ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)), including the `401` written by the JWT bearer middleware and the `429` written by the rate limiter:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

The `traceId` is the request's W3C trace id, so an error report can be matched to the request in distributed tracing. An unknown email and a wrong password on login return the same problem; only the `traceId` differs, as it does between any two requests.

A few choices worth knowing:

- **Every refusal of a refresh is the same `401`**, whether the token is unknown, expired, revoked or reused. The client has one thing to do in all four cases (log in again), and an attacker learns nothing about why the token was refused.
- **`409 Conflict` on refresh** means another request with the same token won the race (two tabs refreshing at once, for example). Nothing was issued; the client should use the token pair from the request that won.
- **Logout does not require an access token.** Holding the refresh token is enough authority to end its session, and a client whose access token already expired can still log out.
- **Login validates only that the password is present**, not its length. The password policy belongs to register; enforcing it on login would lock out users who registered under an older policy.
- **The refresh token travels in the JSON body.** It keeps the API simple to call from any client (mobile apps, scripts, other services) and to test. For a browser app, the stronger option is an `HttpOnly`, `Secure`, `SameSite=Strict` cookie scoped to `/auth`: JavaScript cannot read it, so an XSS cannot steal it, at the cost of CSRF protection and a cookie-aware client. The rotation and reuse detection behind the endpoint stay the same in both designs.

### Rate limiting

Login and register accept **5 requests per minute per client IP** (fixed window, no queue). Past the limit the API answers `429 Too Many Requests` with a `Retry-After` header in seconds. The default status of the ASP.NET Core rate limiter is `503`, which would blame the server; `429` tells the client it sent too much.

Refresh, logout and `/auth/me` are not limited: a 256-bit refresh token cannot be guessed, `/auth/me` already needs a valid access token, and logout has nothing to guess.

What this does **not** cover, on purpose:

- **No per-account lockout.** Locking an account after N wrong passwords lets anyone lock out any user whose email they know.
- **Distributed attacks.** A per-IP limit does not stop many IPs trying in parallel; that needs signals beyond one API (a WAF, bot detection, breached-password checks).
- **Behind a reverse proxy**, every request comes from the proxy's IP, so all clients would share one limit. The API would need `ForwardedHeaders` configured for the trusted proxy before the limit means anything.

## Security logging

The services log security events with `[LoggerMessage]`: each event has a fixed `EventId` and level, and its fields are structured, so alerts and dashboards can filter on them and keep working if the message text changes.

| EventId | Event | Level | Fields |
| - | - | - | - |
| 1001 | Refresh token reuse detected, family revoked | Warning | `UserId`, `FamilyId` |
| 1002 | Session ended by logout | Information | `UserId`, `FamilyId` |
| 1003 | Refresh lost a concurrent race | Information | `UserId`, `FamilyId` |
| 2001 | Login with an unknown email | Warning | — |
| 2002 | Login with a wrong password | Warning | `UserId` |
| 2003 | Login succeeded | Information | `UserId` |

**Never logged:** refresh tokens (neither the raw value nor its hash), access tokens, passwords and emails. Emails are personal data and come straight from the request, so a log line could carry whatever an attacker typed; that is why event 2001 has no fields. Tests run the whole flow and check that none of these values appears in any log message or field.

Events 2001 and 2002 are separate although the client gets the same answer for both (ADR 5): hiding which emails exist is about the response, and the logs are internal. For whoever investigates an attack, whether the target was a real account matters.

Outside development the console writes one JSON object per line, ready for a log collector:

```json
{"Timestamp":"2026-10-31T16:09:40.811Z","EventId":2002,"LogLevel":"Warning","Category":"JwtRefresh.Application.Services.UserService","Message":"Login failed: wrong password for user 0b5e...","State":{"UserId":"0b5e...","{OriginalFormat}":"Login failed: wrong password for user {UserId}"}}
```

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
  JwtRefresh.Application/             RefreshTokenService, UserService, repository, unit of work and token issuer interfaces
  JwtRefresh.Infrastructure/          EF Core mapping, migrations, repositories (PostgreSQL) and the JWT access token issuer
  JwtRefresh.Api/                     Minimal API endpoints, JWT bearer authentication, request validation
tests/
  JwtRefresh.Domain.Tests/            domain unit tests (xUnit)
  JwtRefresh.Application.Tests/       service tests with in-memory fakes and FakeTimeProvider
  JwtRefresh.Infrastructure.Tests/    repository and unit of work tests against PostgreSQL (Testcontainers), token issuer tests
  JwtRefresh.Api.IntegrationTests/    HTTP tests with WebApplicationFactory and a PostgreSQL container
docs/adr/                             Architecture Decision Records
```

## Running the tests

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (or Podman) running. The infrastructure and integration tests start a disposable PostgreSQL container with [Testcontainers](https://dotnet.testcontainers.org/).

```bash
dotnet test
```

GitHub Actions runs the same restore, build and test on every push and pull request to `master` (`.github/workflows/ci.yml`), on `ubuntu-latest`, which has Docker available for Testcontainers. The SDK version comes from `global.json`, the same one used locally.

The whole project was built test-first: every behavior went through a failing test before the implementation, and the commit history keeps each RED → GREEN → REFACTOR step.

The integration tests treat the API as a black box: they create their users through `/auth/register` and check behavior only through HTTP, for example that reusing a rotated token also kills its successor, and that logging out one session leaves the user's other sessions alive. Two cases are covered below the HTTP level on purpose:

- **Expired refresh tokens** are tested in the domain and application tests, where a `FakeTimeProvider` moves the clock. Testing them over HTTP would need a fake clock inside the API host just for one case, and at the endpoint an expired token is one more refusal mapped to `401`.
- **Concurrent refreshes (`409`)** are tested against a real database in the unit of work tests, which run the competing commit at an exact point. Reproducing the race over HTTP would make a flaky test.

The rate limiting tests run on a host built with `WithWebHostBuilder` and a limit of 2, so they get their own limiter and never slow down the other tests, which run with a high limit. The security log events are tested in the application tests with `FakeLogger`, by `EventId`, level and fields. The ProblemDetails body of an unhandled exception (`500`) is not tested: it would need an endpoint that exists only to throw, and outside development ASP.NET Core already leaves the exception details out of the response.

## Running locally

Start the database and apply the migrations:

```bash
docker compose up -d postgres
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/JwtRefresh.Infrastructure --startup-project src/JwtRefresh.Api
```

Then run the API:

```bash
dotnet run --project src/JwtRefresh.Api
```

It listens on `http://localhost:5116`. The development connection string and a development-only signing key are in `src/JwtRefresh.Api/appsettings.Development.json`.

### Configuration

| Setting | Default | Notes |
| - | - | - |
| `AccessToken:Lifetime` | 15 minutes | |
| `AccessToken:Issuer` / `Audience` | `jwt-refresh-api` / `jwt-refresh-clients` | |
| `AccessToken:SigningKey` | none | At least 32 bytes. Never committed for production: set it with the environment variable `AccessToken__SigningKey`. |
| `RefreshToken:Lifetime` | 7 days | Inactivity limit of each token. |
| `RefreshToken:SessionLifetime` | 30 days | Absolute limit of a session since login. |
| `RateLimiting:PermitLimit` | 5 | Login and register requests per window, per client IP. |
| `RateLimiting:Window` | 1 minute | |
| `Logging:Console:FormatterName` | `json` | `simple` in `appsettings.Development.json`, for reading in a terminal. |

All options are validated when the API starts: a missing or short signing key, a session lifetime shorter than the token lifetime, or a rate limit that is not positive stops the API at startup instead of failing on the first request.

To run the API as in production (JSON logs, no development settings), skip the launch profile, which would set the environment back to `Development`:

```bash
ASPNETCORE_ENVIRONMENT=Production \
AccessToken__SigningKey="<at least 32 bytes>" \
ConnectionStrings__Default="Host=localhost;Database=jwt_demo;Username=jwt_demo;Password=jwt_demo" \
dotnet run --no-launch-profile --project src/JwtRefresh.Api
```

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
- [x] JWT access tokens and bearer authentication
- [x] Endpoints: register, login, refresh, logout and a protected `/auth/me`
- [x] Integration tests with `WebApplicationFactory` and Testcontainers
- [x] ProblemDetails (RFC 9457) for every error response
- [x] Rate limiting on login and register
- [x] Structured security logging, with tests that no secret reaches the logs

## License

[MIT](LICENSE)
