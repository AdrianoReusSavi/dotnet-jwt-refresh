# 2. Separate used and revoked markers, with reuse taking precedence

## Context

With rotation, every refresh exchanges the current token for a new one. The old token stops being valid, but there are different reasons for a token not to be valid:

- it has already been **used** in a rotation;
- it has been **revoked** by a logout or by a family revocation;
- it has **expired**.

A single field, such as `IsValid` or `Status`, would keep only the most recent reason and lose the others. And the difference between these reasons matters a lot: a **used** token that shows up again means someone else has a copy of it. A legitimate client never resends a token it has already exchanged.

It is also necessary to decide what happens when more than one reason applies at the same time, for example a used token that has also expired.

## Decision

- The token keeps three independent markers: `ExpiresAt`, `UsedAt` and `RevokedAt`. Used tokens are **not deleted**, because they are what makes reuse detectable.
- `Rotate` checks in this order: **used → revoked → expired**. The first one that applies defines the result (`ReuseDetected`, `Revoked` or `Expired`).
- **Reuse wins over everything.** Picture an attacker who steals a token, lets the victim rotate it, and only tries to use it 8 days later. If the answer were `Expired`, the system would treat it as routine and not react, even though the victim's family is still active. A used token that reappears proves the leak, whether it is expired or revoked.
- **Revoked wins over expired.** Revocation is an explicit action and says more during an investigation than a deadline that simply passed.
- **A refused rotation changes nothing:** it hands out no new token and does not touch `UsedAt`. The time of the first use is the clue to when the token was legitimately exchanged.
- **`Revoke` is idempotent** and keeps the first time. Revoking the whole family, or two requests revoking at the same time, never raises an error.

## Consequences

- **Positive:** the system tells an attack apart from a logout, and each token's history is preserved for investigation. The behavior for every combination of states is defined and covered by tests.
- **Negative and caveats:** the table grows, because used tokens are kept. A periodic cleanup of tokens from sessions that have already ended will be needed.
- **Pending:** the domain only **signals** reuse. The reaction (revoking every token in the family) belongs to the application layer, in the next step.
