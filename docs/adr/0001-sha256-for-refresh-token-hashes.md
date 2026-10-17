# 1. SHA-256 for refresh token hashes

## Context

The refresh token is issued at login and exchanged for a new one on every refresh. While it is valid, it is as powerful as the password: whoever holds the value can get new access tokens without logging in. So it cannot be stored in plain text. If the database leaks, the active tokens must be useless to the attacker.

The solution is to store only a **hash** of the token. Hashing is not encryption: there is no key, and the original value cannot be recovered. The server receives the token from the client, computes its hash and looks that hash up in the database.

Passwords in this project already use BCrypt, so the first option would be to use it for tokens too. It does not fit here:

- BCrypt generates a **random salt** for each hash, so the same token produces a different hash every time. A `WHERE token_hash = @hash` lookup is impossible. The server would have to load the tokens and check them one by one.
- BCrypt is **slow on purpose**. That is good against brute force on passwords, but checking several tokens on every refresh would be expensive.

## Decision

The refresh token is generated from 32 bytes of `RandomNumberGenerator` (256 bits), encoded as Base64Url and handed to the client. The database stores only the `SHA-256` of that value, in hexadecimal (`RefreshToken.Hash`).

SHA-256 is safe **for this case**, but it would not be for passwords:

- Passwords have **low entropy**. People pick words, dates and patterns, so an attacker can test billions of candidates per second against SHA-256. That is why passwords need a slow, salted hash.
- The token has **256 random bits**. There is no list of likely candidates: trying every possible value is infeasible with any hash, slow or fast. A salt adds nothing either, because two identical tokens do not happen in practice.

## Consequences

- **Positive:** a database leak does not expose usable tokens. The lookup is direct and can use an index. The hash is fast and deterministic, which makes testing easy.
- **Negative and caveats:** security depends entirely on the token's randomness. If the token ever becomes low-entropy (short, predictable or derived from user data), this decision no longer holds and must be revisited.
- **Alternative considered:** HMAC-SHA256 with a server-side secret would protect even against an attacker who can write to the database. It was left out because it requires key management (storing and rotating the secret) with no meaningful gain for 256-bit tokens.
