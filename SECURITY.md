# Security Policy

## Reporting a vulnerability

Report security issues privately through **Security → Advisories → Report a vulnerability** in the GitHub
repository. Do not open a public issue. Include the affected version or commit, impact, reproduction steps
and any suggested mitigation. The project supports the latest deployed version from `master`.

## Security model

The application assumes a same-origin deployment behind a trusted TLS reverse proxy. The browser reaches
nginx, which serves the SPA and forwards `/api` to the private API container. Cross-origin API use and JWT
authentication are not supported.

### Authentication and authorization

- Authentication uses an ASP.NET Core session cookie: `HttpOnly`, `SameSite=Lax`, non-sliding, expiring after
  eight hours by default. In Production it is `Secure` and uses a `__Host-` name. The ticket is not
  self-sufficient: completing the second factor also writes a `user_sessions` row whose id travels in the
  ticket's `sid` claim, dropping that user's already-expired rows; a background worker additionally deletes
  every expired row on the `SessionCleanup:IntervalMinutes` schedule. The row carries the expiry that decides
  access: refreshing the claims re-issues the cookie with a later expiry, so a cookie can outlive its row,
  and the ticket is rejected as soon as the row is missing or expired.
- Every authenticated request revalidates account status, password fingerprint, administrator flag and the
  `sid` row in one query, and rejects the ticket when any of them is missing or expired. Blocking or demoting
  a user takes effect on existing sessions; changing or resetting the password invalidates them and deletes
  that user's session rows.
- `POST /api/auth/logout` deletes the row of the presented session before clearing the cookies, so a copy of
  that cookie stops working immediately instead of lasting until its expiry. It is idempotent: it asks for no
  valid session, only the CSRF token, and always answers 204 after clearing the session and challenge
  cookies, including when the row is already gone or its deletion fails, which is logged. Deleting an account
  removes its rows by cascade. Tickets issued before this behaviour existed carry no `sid`, so those users
  are signed out once and log in again.
- Authorization is a boolean administrator flag, not a role system. `[AllowOnlyAdmin]` protects
  administration endpoints; `[AllowOnlySelf]` accepts the target user or that user's guardian. Catalog values
  such as `UserType` are not authorization roles, with one handler-level exception:
  `GET /api/events/{eventId}/signup-stats` requires a session and returns `AccessDenied` (403) unless the
  caller is an administrator or has the member `UserType`. Its response is aggregate counts per activity,
  activity role and signup status; it never lists the signed-up individuals. For an activity with very few
  signups, a member can infer an individual's status — including a rejection — from these counts; whether
  this granularity is acceptable for the member audience is a pending decision for the project owner, not
  resolved by this document.
- Granting the administrator flag requires the acting administrator to re-enter their password (a stolen
  session cookie alone cannot promote another account); a wrong password returns
  `UserCurrentPasswordIncorrect` and changes nothing. Revoking needs no password, but the last administrator
  cannot be demoted. Public registration never grants administrator access; on an empty database, startup
  creates the first administrator from `BOOTSTRAP_ADMIN_EMAIL`/`BOOTSTRAP_ADMIN_PASSWORD`, ignored once a
  user exists.
- `PUT /api/users/{id}` asks the caller (the user, their guardian or an administrator) for their own
  password whenever the update would replace the account's login identifiers: a different email or phone.
  A missing or wrong password returns `UserCurrentPasswordIncorrect` and changes nothing; edits that leave
  both identifiers untouched need none. A new address is stored as given and is not confirmed by an emailed
  code. The stored account, never the request, decides the rest: an account that is not already a dependent
  is refused a guardian (`UserParentNotAllowedForAdult`) and a minor birth date (`UserCannotBecomeMinor`)
  and keeps its credentials, so no request can demote an account into somebody's dependent; a dependent
  only accepts its own guardian repeated or omitted (`UserParentReassignmentForbidden` otherwise) and is
  never reassigned. Giving a dependent an adult birth date releases it into a standalone account, which
  therefore requires its own email and phone and the caller's password, like any other identifier change.
  Dependents are created only through `POST /api/users/{id}/children`.

### Two-factor authentication

Every account logs in in two steps; there is no opt-out. `POST /api/auth/login` verifies the password and
issues a short-lived challenge cookie (`__Host-CodigoActivo.TwoFactor` in Production, 10 minutes,
non-sliding) instead of a session; it is bound to the password fingerprint and account status, so a password
change or block invalidates it. The session cookie is only issued by `POST /api/auth/login/two-factor` once
the second factor is accepted, which is also when the login timestamp is recorded.

Each user chooses one second factor from their account:

- **Email (default)**: a 6-digit code, emailed on the password step, stored hashed with Argon2id, expiring
  with the challenge, single use. The password step reuses a code sent within the last minute instead of
  resending; `POST /api/auth/login/two-factor/resend` enforces the same 60-second cooldown. These emails
  count as credential mail in the automatic-message limiter.
- **Authenticator application**: RFC 6238 TOTP (HMAC-SHA1, six digits, 30-second steps, one step of clock
  drift) via Otp.NET; the project implements no cryptographic algorithm itself. Enrollment starts with the
  user's password, stores the shared secret encrypted with ASP.NET Data Protection, and only activates once
  the first code is confirmed; an unconfirmed enrollment expires after 15 minutes. Each accepted time step is
  remembered so a code cannot be replayed. Returning to email requires the password and a current code.

Wrong codes are counted per account across logins and authenticator removal: after five failures the second
factor locks for 15 minutes (`TwoFactorLocked`). A successful code resets the counter. Enrollment URIs and
shared keys are only returned to the authenticated owner over HTTPS and never logged.

**Account deletion**: erasing one's own account requires the current password **and** the second factor on
`POST /api/me/deletion` (email code via `POST /api/me/deletion/code`, sharing storage/lifetime/cooldown with
the login code, or the authenticator code). Deletion signs the caller out, cascades to remove the user, their
minors and all participation rows (past ratings stay, see [Event rating anonymity](#event-rating-anonymity)),
is refused with `UserDeleteAuthoredContentExists` while content credits the household, and is blocked for
administrators (`UserDeleteAdminForbidden`). `DELETE /api/users/{id}` refuses the caller's own id
(`UserSelfDeleteRequiresVerification`).

**Recovery**: an administrator can reset a user's second factor to email
(`POST /api/users/{id}/two-factor/reset`) after re-entering their own password, which also clears any
lockout. Losing the Data Protection keys makes stored authenticator secrets unreadable; affected users then
need the same administrator reset.

### CSRF

`CsrfValidationMiddleware` validates every unsafe HTTP method. The client obtains a token from
`GET /api/auth/csrf` and returns it in `X-CSRF-TOKEN`; the SPA client handles this automatically.

### Passwords and credential endpoints

Passwords require 12–128 characters and are hashed with Argon2id, never stored or logged in plaintext. Login
performs fallback Argon2 work for unknown identifiers to reduce timing differences.

Five consecutive wrong passwords for the same account — counted together by `PasswordAttemptGuard` across the
login password step and every route that asks the caller to re-enter their own password — lock the account,
delete its `user_sessions` rows and email its owner; one correct password before the limit clears the count.
A locked account answers every password, right or wrong, with the same `InvalidCredentials` after the same
Argon2 work, and only a completed password reset lifts it: `forgot-password` keeps working, while an
administrator's second-factor reset does not unlock. The accepted trade-off is that anyone who knows an
account's email or phone can lock it, so recovery depends on the owner's mailbox.

Credential routes (both login steps and code resend, registration, verification, password recovery/change,
authenticator enrollment/removal, administrator grants, second-factor resets, user updates and the two
self-service account-deletion steps) have layered controls:

- nginx caps `login`, `register`, `forgot-password`, `/api/auth/{id}/(verify|resend-verification|reset-password)`
  and `/api/users/{id}/(password|admin)` at 10 requests/second per client IP, burst 100; the remaining
  credential routes (two-factor login/resend/authenticator/email, `users/{id}`,
  `users/{id}/two-factor/reset`, `me/deletion*`) rely only on nginx's general 200 requests/second limit and
  the API controls below.
- the API enforces 120 requests/minute per client IP in every environment on every credential route;
- memory-hard credential work has a process-wide concurrency limit of 4 and a FIFO queue of 16.

Normal API traffic uses different limits, since an IP may represent many legitimate users: nginx allows 200
requests/second per IP, burst 500; the API applies a sliding one-minute budget of 300 requests per
authenticated user, or 3,000 per IP for anonymous traffic, with 128 requests executing and 256 waiting
process-wide. Report routes add a 30-request/minute budget per user (16 executing, 32 waiting); multipart
file writes allow 30 requests/minute per user (12 executing, 12 waiting). Rejected requests include
`Retry-After`. These are per-instance controls; scaling API replicas multiplies aggregate capacity.

### Input, output and error handling

- DataAnnotations and custom attributes validate request models. JSON bodies accept enum values only by
  their declared name, never as integers, and enums bound from the query string reject undefined values.
- Expected failures use a string `ErrorCode`; all failures return
  `ApiErrorResponse(Title, Status, Code, TraceId)` without stack traces or internal details.
- User-supplied links allow only absolute HTTP(S) URLs without embedded credentials.
- Rich-text JSON is checked against node, mark, attribute, nesting and size allowlists; embedded images may
  only reference this application's UUID-based file endpoint.
- User-controlled values inserted into email templates are HTML-encoded; administrator-authored bodies are
  plain text rendered inside the branded template.

### Transport and proxy trust

In Production the API accepts one forwarded hop, redirects HTTP to HTTPS and emits secure cookies.
`X-Forwarded-For`/`X-Forwarded-Proto` are honoured only from loopback and private ranges (`127.0.0.0/8`,
`::1/128`, `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `fc00::/7`); other peers are ignored. The base
Compose file keeps `api` and `db` off host ports but publishes nginx as `8080:8080` on all interfaces. The
operator must terminate TLS externally, overwrite untrusted `X-Forwarded-For`/`X-Forwarded-Proto`, and
prevent clients from bypassing the proxy to reach port `8080` directly. nginx sends HSTS only when the
forwarded scheme is HTTPS, plus CSP, frame denial, MIME sniffing, referrer, permissions and cross-origin
isolation headers. See [DEPLOYMENT.md](DEPLOYMENT.md#tls-and-proxy-boundary).

### Privacy and third parties

- Browsers load nothing from third parties: fonts are bundled from Fontsource and served from the application
  origin; the CSP allows scripts, styles, fonts, images and connections only from `'self'` (plus
  `data:`/`blob:` images). Do not add CDNs, analytics or embeds without a legal basis and consent review.
- The only cookies are the session, two-factor challenge and CSRF cookies above; the theme choice stays in
  `localStorage` and is never sent to the server. `Referrer-Policy: same-origin` keeps page URLs out of
  requests to external sites.
- Terms-document consent is recorded for the acting user, not the enrolled person: enrolling a household
  minor stores the guardian's decision in `event_terms_acceptances`, and the guardian's earlier acceptance
  for that event covers later minor signups without a new prompt (`TermsGate`). An acceptance is immutable
  and never re-asked; a rejection is revisable and overwrites the stored row; a required document's
  rejection is never persisted. `AssignActivity` skips this consent step when an administrator enrolls
  someone else; `AssignHousehold` always runs it. The acceptance row cascades away when the acting user's
  account is deleted.
- Verification and password-reset links put the user id and a 256-bit code from a cryptographic random
  generator in the URL fragment (`/reset-password#userId=…&code=…`), which browsers never send to the server;
  the page reads and removes it from the address bar, so a reload needs the emailed link again.

### Event rating anonymity

Event ratings use two unrelated tables: `event_rating_submissions` tracks only who rated an event (composite
`event_id`/`user_id` key); `event_ratings` stores the answer under a random v4 `Guid`, with no user id or
timestamps. `POST /api/events/{eventId}/rating` writes both rows in one transaction and rejects a second
submission per event/user (`EventRatingAlreadySubmitted`); listings never expose the author.

Account deletion keeps past ratings, since their content carries no author reference. `AnonymizeEventRatings`
retrofitted this onto previously linked data (moved `user_id` into the submission table, dropped
`user_id`/`created_at`/`updated_at` from `event_ratings`, reinserted every row in random order, ran
`CLUSTER`); its `Down` migration throws, since the author link was discarded. `SubmitAsync` repeats this
reshuffle on every later submission, defending the API, admin UI, ordinary queries and any single post-commit
dump.

**Known limitations:** PostgreSQL write-ahead log access (`pg_wal`, in volume copies and point-in-time
recovery); dead tuples until the next `VACUUM`; PostgreSQL statement logs if `log_statement` is enabled;
backups/dumps taken **before** the migration, which still link ratings to authors (rotate them out, see
[DEPLOYMENT.md](DEPLOYMENT.md#backups-and-recovery)); differential observation of two dumps taken
before/after a submission; and, for events with one or two ratings, deducible authorship from count or
free-text content (no minimum-rating threshold exists). Logs also correlate: API security logs record the
user id and time of every login (see [Logging](#logging)), while the nginx access log records client IP, time
and request paths that can contain user ids, so an operator holding both can link an IP to an account and a
rating submission request to a person. The stored rating content still carries no author reference.

### Logging

Never logged: names, emails, phone numbers, birth dates, login identifiers typed by the client,
passwords/hashes, one-time codes, TOTP secrets, cookies, CSRF tokens, request bodies, query strings, Referer,
and SMTP replies quoting a recipient or message. Logged: HTTP method, route path (GUID parameters), status,
timing, entity ids, enum values, email kind, counts, SMTP status codes, and exception messages/stack traces.

- **Security events**: handlers and the session plumbing emit the events declared in
  `Application/Auth/SecurityLog.cs` — accepted password steps, completed logins and ended sessions, failed
  password and second-factor steps, lockouts, refused logins, password changes and resets, authenticator and
  administrator changes, wrong re-authentication passwords, identifier changes and deletions — at `Warning`
  for failures and `Information` for completed steps and changes, carrying only entity and catalog ids, enum
  values and counts. Successful and failed authentication steps alike are recorded with the user id, the
  second-factor method and the time, so the logs show when a given account signed in, was tried or signed out.
- **API**: `RequestLoggingMiddleware` logs 4xx/5xx only. Only `CodigoActivo`, `Program` and
  `Microsoft.Hosting.Lifetime` log at `Information`; other categories log at `Warning`. EF Core
  sensitive-data logging stays disabled; `SmtpEmailSender` strips recipient/server replies from exceptions.
- **nginx**: access log keeps IP, time, method, path without query, status, size, upstream status and user
  agent, not Referer; error log is limited to `crit`.
- **PostgreSQL**: `log_error_verbosity=terse` drops `DETAIL` lines; failing statements show `$n` placeholders.
- Container logs are rotated (see [DEPLOYMENT.md](DEPLOYMENT.md#production-topology)).

### Production configuration and secrets

Production startup fails when `POSTGRES_PASSWORD` is under 16 characters, `DATA_PROTECTION_CERTIFICATE_PASSWORD`
is under 32, `APP_BASE_URL` is not a clean public HTTPS origin, SMTP host/port/sender/transport encryption is
invalid, or only one of `SMTP_USERNAME`/`SMTP_PASSWORD` is set.

Secrets are flat environment variables in the ignored root `.env`; `appsettings.json` contains no
credentials. Restrict `.env` to the deployment account and keep it out of images and unencrypted backups.
Rotating the database password must update both the PostgreSQL role and the API configuration. Keep the Data
Protection certificate password separate from the key-volume backup.

### Data Protection and containers

Production persists ASP.NET Data Protection keys in `api-dataprotection`, encrypted and signed with a
locally generated Ed25519 certificate protected by `DATA_PROTECTION_CERTIFICATE_PASSWORD`. Application
containers run as non-root, drop all capabilities, enable `no-new-privileges` and use read-only root
filesystems; PostgreSQL is reachable only on the internal backend network. The development override removes
parts of this boundary and must not be deployed.

### Files and multipart requests

Stored uploads are limited to 10 MiB by default and saved under `/app/files` in the `api-files` volume; nginx
also enforces a `12m` request-body limit. Access to stored files goes through API authorization, not a direct
static volume mount. Administrator email attachments are transient — read into the outbound message and
discarded, never stored — with count, combined bytes and recipient count bounded by application settings.

## Email abuse controls

New accounts must always confirm an emailed OTP before their first login; there is no switch. Verification
and password-reset codes expire after 15 minutes with a 60-second resend cooldown; login and account-deletion
codes share storage and expire after 10 minutes with the same cooldown. SMTP must be configured everywhere or
the API refuses to start. Activity signup sends no message; confirming or rejecting one queues an outcome
email after commit, always to the guardian address for a dependent minor. Delivery errors never roll back
registration, recovery, activity decisions or security changes.

Changing an account's password (by the user or through recovery), its second factor (authenticator confirmed,
returned to email, or reset by an administrator), its administrator flag or its login identifiers queues a
notification to the affected account after commit, from the handler, whoever asked for the change. The notice
names the change and its timestamp and carries no code, secret or link that performs an action; an identifier
change is announced to the **previous** address and only ever quotes the new one masked. These messages are
ordinary automatic mail, not credential mail, so they spend the shared budget without touching the credential
reserve that login codes rely on.

Every automatic verification, password-reset, second-factor-code, security-change and activity-decision email
passes through `ThrottledEmailSender`: each normalized destination has burst/hourly/daily budgets, the
process has a global budget with a credential-email reserve, and normalization lowercases addresses, strips
sub-address tags and folds dots only for Gmail/Googlemail. Quota is spent on attempt, before queueing; SMTP
failure and a full queue do not refund it. The limiter is always enabled, in-memory, resets on restart and is
multiplied by the number of API replicas.

Accepted mail enters a bounded in-memory channel drained by a fixed worker pool with no retry or persistence;
graceful shutdown drains it within 20 seconds by default. SMTP failures are logged per message. Operational
settings and log signals are in [DEPLOYMENT.md](DEPLOYMENT.md#email-delivery).

Only administrators may use `/api/emails/...` endpoints; recipients are resolved on the server from the same
filters as user/attendee lists, never client-supplied addresses. Each recipient gets a separate message,
minors without their own address are skipped, and attachments remain transient. This mail bypasses the
automatic-message limiter and is delivered synchronously. Single-recipient messages allow 30 requests/minute
per administrator (10 executing, 10 waiting); bulk messages allow 5 requests/minute (2 active, no waiting
queue). All requests remain subject to nginx's general API limit, application recipient/attachment limits and
the five-minute nginx upstream timeout.

## Demo mode

`DEMO_MODE=true` seeds invented accounts with a random, hashed and discarded 32-byte password, fictitious
addresses, and the second factor still required. Demonstrations use the bootstrap administrator configured
in Compose. Such deployments are disposable and must never contain private data. The first selected mode is
persisted in `api-state`; a later conflicting value stops startup. Changing it requires deleting all named
volumes, see [DEPLOYMENT.md](DEPLOYMENT.md#demo-mode-and-initial-administrator).

## Operational checklist

- Keep the TLS proxy as the only public ingress and restrict direct port `8080` access.
- Back up and test restoration of the database, uploads, mode state and Data Protection material.
- Alert on authentication abuse, email-budget exhaustion, queue saturation and SMTP delivery failures.
- Review administrator access; smoke-test session revocation, file authorization and account recovery after
  releases.
- Treat the development override, the bootstrap credentials of demo deployments and any copied production
  `.env` as sensitive operational risks.
