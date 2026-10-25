# 5. User enumeration on login and register

## Context

*User enumeration* means finding out which emails have an account. An attacker who has a list of registered emails can focus password guessing on them, or use them for targeted phishing.

The API can leak this in two places:

- **Login.** If "unknown email" and "wrong password" get different answers, the answer itself tells whether the email exists. Even with the same answer, the *time* can leak it: a known email runs BCrypt to check the password (about 100–200 ms), and an unknown email would return almost immediately.
- **Register.** When the email is already taken, the register has to fail somehow, and that failure tells the caller that the email exists.

## Decision

**Login hides the difference completely.**

- An unknown email and a wrong password return the same result: `InvalidCredentials`, with no user id.
- When the email is unknown, `UserService` still checks the password against a fixed dummy BCrypt hash and throws the result away. Both failure paths spend the same BCrypt time, so the response time does not reveal the email either.
- The dummy hash is computed once (`static readonly`), so it costs one hash at startup and nothing per request.

**Register accepts the leak, and returns `EmailTaken`.**

- It is checked twice: a lookup before inserting, and the unique index `ux_users_email` at commit time. The index catches two registers with the same email that arrive at the same time and both pass the lookup. `UnitOfWork` translates the unique violation into `UniqueConstraintViolationException`, and the service returns `EmailTaken` for it too.

## Why register is allowed to leak

The only way to hide it is to answer "check your inbox" for every register and send the confirmation (or a "you already have an account" notice) by email. That needs an email provider, confirmation tokens and a pending-account state, which are outside the scope of this project.

Without email confirmation, a register that hid the conflict would leave the user unable to tell why they cannot log in. Large sites also answer "email already in use" in the sign-up form, and rely on rate limiting to make enumeration through it slow and noisy.

## Consequences

- **Positive:** login, which is the endpoint attacked most often and in bulk, reveals nothing, neither in the answer nor in the time.
- **Positive:** a concurrent duplicate register gets the same `EmailTaken` as a sequential one, instead of a 500 error.
- **Negative:** register can be used to test whether an email has an account. The mitigation is rate limiting on the register endpoint (see the Roadmap).
- **Negative:** the timing protection has no automated test. A timing test would depend on the machine and its load, and `PasswordHasher` is static, so the call cannot be observed with a fake. A comment next to the dummy hash explains why the line exists, so it is not removed as dead code.
