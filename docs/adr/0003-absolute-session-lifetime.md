# 3. Absolute session lifetime

## Context

Every refresh issues a new token with the full lifetime, for example 7 days. So someone who uses the system often never has to log in again: each rotation pushes the expiration forward.

That is convenient for the user, but it also works for an attacker. If they steal a refresh token and use it before the victim does, they become the one rotating the family. The victim just gets "invalid token" and logs in again, which creates a new family. Without any limit, the attacker's family lives forever, as long as they refresh every 7 days.

## Decision

There are two different deadlines:

| Deadline | What it measures | Where it lives |
| - | - | - |
| **Inactivity** | how long the user can go without using the system | each token's `ExpiresAt` (`lifetime`) |
| **Absolute** | how long the session can last since login, whether used or not | `SessionExpiresAt` (`sessionLifetime`) |

- `SessionExpiresAt` is computed **once**, in `Issue`.
- `Rotate` passes `SessionExpiresAt` on to the new token **without recomputing it**, so the limit never moves forward.
- Each token expires at whichever comes first: `ExpiresAt = Earliest(createdAt + lifetime, SessionExpiresAt)`. The last token of the session expires exactly at the limit, not 7 days after it.
- After the limit, the token is expired and `Rotate` returns `Expired`.

## Consequences

- **Positive:** a stolen token grants access for, at most, the time left in the session. The rule lives in the constructor, so no token can be created with an expiration past the limit, whether it comes from `Issue` or `Rotate`.
- **Negative:** even an active user has to log in again when the session reaches its limit (for example, every 30 days).
