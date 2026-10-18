# 4. Optimistic concurrency with xmin

## Context

Each refresh request loads the token, calls `Rotate` and commits two changes in one transaction: the `UPDATE` that sets `used_at` and the `INSERT` of the successor. The transaction makes those two changes atomic, but it does not stop **two requests** from reading the same token at the same time.

If two requests present the same token at the same moment (a double click, two browser tabs, or an attacker racing the victim), both load it while it is still active, both rotate it, and both commit. The family ends up with **two active successors**, and reuse detection never fires, because neither request saw a used token.

The same gap exists between a `Revoke` and a `Rotate`: a logout and a refresh that happen together could leave the token revoked **and** with an active successor. The logout would not log anyone out.

## Decision

- Use **optimistic concurrency** on `refresh_tokens`. Every `UPDATE` only applies if the row is still in the version that was read. If another request changed it first, the `UPDATE` affects no rows, EF Core throws `DbUpdateConcurrencyException` and the whole transaction is rolled back, including the successor's `INSERT`.
- The version is PostgreSQL's **`xmin`** system column, mapped as a shadow property (`builder.Property<uint>("xmin").IsRowVersion()`). Every table already has it, so the migration does not change the schema, and the domain does not get a property that only exists for the database.
- **Why not mark `UsedAt` as the concurrency token:** that would only detect conflicts on `used_at`. A `Revoke` changes `revoked_at` only, so a concurrent `Rotate` would still pass its `WHERE used_at IS NULL` check and issue a successor. `xmin` changes on any update to the row, so it catches every combination.
- **A request that loses the race is a refused refresh, not a reuse.** It does not get a new token, and the family is **not** revoked. The losing request read the token while it was active; nothing proves that a copy leaked.

## Consequences

- **Positive:** a token can produce at most one successor, and a logout always wins over a concurrent refresh. Both cases are covered by tests against a real PostgreSQL.
- **Treating the loser as a race does not open a hole.** The loser gets nothing. If it was the victim, and the attacker won the race, the victim's next attempt with the same token finds it used and returns `ReuseDetected`, which revokes the family. Reuse detection still catches the leak, one request later.
- **Negative:** a legitimate client that refreshes from two tabs at once gets an error in one of them, and if that tab retries with the old token, it triggers `ReuseDetected` and ends the session. The client should send one refresh at a time. Some providers accept the previous token for a short grace period instead; that was left out because it adds state and widens the window for a stolen token.
- **Amends [ADR 2](0002-separate-used-and-revoked-markers.md):** `Revoke` is still idempotent in the domain, but two concurrent revokes now conflict at the database. The application layer must catch the `DbUpdateConcurrencyException`, reload the token and revoke again, which is a no-op, so the caller never sees an error.
