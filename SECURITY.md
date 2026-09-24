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

- Authentication uses an ASP.NET Core session cookie: `HttpOnly`, `SameSite=Lax`, persistent (it survives
  closing the browser), non-sliding, expiring after 30 days by default. In Production it is `Secure` and uses
  a `__Host-` name. The ticket is not
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
  removes its rows by cascade.
- Authorization is a boolean administrator flag, not a role system. `[AllowOnlyAdmin]` protects
  administration endpoints; `[AllowOnlySelf]` accepts the target user or that user's guardian. Catalog values
  such as `UserType` are not authorization roles, with one handler-level exception:
  `GET /api/events/{eventId}/signup-stats` requires a session and returns `AccessDenied` (403) unless the
  caller is an administrator or has the member `UserType`. Its response is aggregate counts per activity,
  activity role and signup status; it never lists the signed-up individuals. For an activity with very few
  signups, a member can infer an individual's status — including a rejection — from these counts; whether
  this granularity is acceptable for the member audience is a pending decision for the project owner, not
  resolved by this document.
- The email is the only login identifier and the only unique personal value. The DNI/NIE, the phone and
  the optional secondary phone are not unique, so no route checks them against other accounts and neither
  registration nor profile updates reveal whether someone else uses them; the secondary phone only has to
  differ from the same account's phone (`SecondaryPhoneSameAsPrimary`). `POST /api/auth/login` resolves the account by email only.
- **Known limitation**: `POST /api/auth/register` accepts anonymous requests, so an attacker who knows
  someone else's email can register with it; the account is created pending verification and the email
  stays reserved until an administrator deletes it. The 409 (`RegisterEmailAlreadyInUse`) also lets a
  caller probe whether a given email is already registered.
- Granting the administrator flag requires the acting administrator to re-enter their password (a stolen
  session cookie alone cannot promote another account); a wrong password returns
  `UserCurrentPasswordIncorrect` and changes nothing. Revoking needs no password, but the last administrator
  cannot be demoted. Public registration never grants administrator access; on an empty database, startup
  creates the first administrator from `BOOTSTRAP_ADMIN_EMAIL`/`BOOTSTRAP_ADMIN_PASSWORD`, ignored once a
  user exists.
- `PUT /api/users/{id}` asks the caller (the user, their guardian or an administrator) for their own
  password whenever the update would replace the account's email, phone or secondary phone. A missing or
  wrong password returns `UserCurrentPasswordIncorrect` and changes nothing; edits that leave all three
  untouched need none.
  Changing the DNI/NIE needs no password. Only a different email can be refused as already in use
  (`UserEmailAlreadyInUse`). A new address is stored as given and is not confirmed by an emailed code. The stored account,
  never the request, decides the rest: an account that is not already a dependent is refused a guardian
  (`UserParentNotAllowedForAdult`) and any birth date (`UserBirthDateNotAllowedForAdult`), must supply a
  DNI/NIE (`UserNationalIdRequired`) and may set `promotionalConsent`,
  so no request can demote an account into somebody's dependent; a dependent only accepts its own guardian
  repeated or omitted (`UserParentReassignmentForbidden` otherwise) and is never reassigned, must supply a
  birth date (`UserChildBirthDateRequired`) that stays a minor's only when it changes from the stored value
  (`UserChildBirthDateNotMinor`), so a dependent who already turned 18 stays editable with their stored
  birth date, and never stores a DNI/NIE or promotional consent; turning 18 changes nothing about the
  account itself, which keeps the `Dependent` status and no password, so it still cannot log in
  (`UserAccountIsDependent`) and `forgot-password` still ignores it. Dependents are created only through
  `POST /api/users/{id}/children`, must be minors at creation, and leave their guardian only when the
  guardian deletes them.

### Two-factor authentication

Every account logs in in two steps; there is no opt-out. `POST /api/auth/login` verifies the password and
issues a short-lived challenge cookie (`__Host-CodigoActivo.TwoFactor` in Production, 10 minutes,
non-sliding) instead of a session; it is bound to the password fingerprint and account status, so a password
change or block invalidates it. The ticket is not self-sufficient either: the password step stores a random
challenge id on the account and puts it in the ticket, so a copied challenge cookie is refused as soon as
that id changes or is cleared — a newer password step rotates it, and an accepted second factor, a
second-factor lockout, a password lockout, a password change or reset and signing out all clear it, making
each challenge single-use instead of valid for its whole 10 minutes. A challenge obtained just before the
account was locked by wrong passwords is refused at both the ticket and the handler. Reading or resending the current challenge keeps
working across reloads. The session cookie is only issued by `POST /api/auth/login/two-factor` once the
second factor is accepted, which is also when the login timestamp is recorded.

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
is refused with `UserDeleteAuthoredContentExists` while content credits the household, and is blocked only
for the last administrator (`UserDeleteLastAdminForbidden`). `GET /api/me/deletion` tells the caller whether
they may delete their account, so the SPA hides the action from the last administrator. The last-administrator
count is not locked against a concurrent deletion or demotion. `DELETE /api/users/{id}` refuses any
administrator (`UserDeleteAdminForbidden`) and the caller's own id (`UserSelfDeleteRequiresVerification`).

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
account's email can lock it, so recovery depends on the owner's mailbox.

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
`::1/128`, `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `fc00::/7`); other peers are ignored. nginx applies
no peer filtering of its own: it normalizes whatever `X-Forwarded-Proto` it receives before forwarding it to
the API — only an exact `https` value (case-insensitive) counts; any other value, a comma-separated list such
as `https, http`, or a missing header become `http` — so the external TLS proxy must overwrite the header
rather than append to it. The base
Compose file keeps `api` and `db` off host ports but publishes nginx as `8080:8080` on all interfaces. The
operator must terminate TLS externally, accepting only TLS 1.3 with TLS 1.2 as the sole fallback, overwrite
untrusted `X-Forwarded-For`/`X-Forwarded-Proto`, and prevent clients from bypassing the proxy to reach port
`8080` directly. nginx sends HSTS (`max-age=63072000; includeSubDomains`, no `preload`) only when its
normalized scheme is HTTPS, plus CSP, frame denial, MIME sniffing, referrer, permissions and cross-origin
isolation headers; `includeSubDomains` requires every subdomain of the public host to work over HTTPS only.
Kestrel does not emit a `Server` header (`AddServerHeader=false`); nginx still sends
`Server: nginx` without a version (`server_tokens off`). See
[DEPLOYMENT.md](DEPLOYMENT.md#tls-and-proxy-boundary).

### Privacy and third parties

- Browsers load nothing from third parties: fonts are bundled from Fontsource and served from the application
  origin; the CSP allows scripts, styles, fonts, images and connections only from `'self'` (plus
  `data:`/`blob:` images), sets `base-uri 'none'` and adds `upgrade-insecure-requests` only when nginx's
  normalized scheme is HTTPS. Do not add CDNs, analytics or embeds without a legal basis and consent review.
- The only cookies are the session, two-factor challenge and CSRF cookies above; the theme choice stays in
  `localStorage` and is never sent to the server. `Referrer-Policy: same-origin` keeps page URLs out of
  requests to external sites, and `X-DNS-Prefetch-Control: off` stops browsers from pre-resolving link
  destinations. API responses whose body is `application/json` or `application/problem+json` also carry
  `X-Robots-Tag: noindex, nofollow`, so search engines do not index API payloads.
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

`event_ratings` stores the answer under a random v4 `Guid`, with no user id or timestamps: `id`, `event_id`,
`score`, `most_liked`, `least_liked`, `suggestions`. `POST /api/events/{eventId}/rating` requires the event
to exist and have ended and the caller (or a dependent) to have confirmed attendance, but does not track who
already rated an event: the same attendee may submit more than once, and every accepted call appends one row
through the standard repository-plus-`IUnitOfWork` write path. There is no per-user submission cap besides
the general 300-requests/minute-per-authenticated-user budget described above; every accepted submission,
repeats included, counts toward that event's rating count and average.

Account deletion keeps past ratings, since their content carries no author reference. Ratings are plain
inserts, so `event_ratings`' physical row order (and `xmin`) reflects write order.

**Known limitations:** PostgreSQL write-ahead log access (`pg_wal`, in volume copies and point-in-time
recovery); dead tuples until the next `VACUUM`; differential observation of two dumps taken before/after a
submission; and, for events with one or two ratings, deducible authorship from count or free-text content (no
minimum-rating threshold exists). Stored logs do not correlate an IP with an account or a rating request (see
[Logging](#logging)): the only stored logs are the API's, which carry no client IP, requested path or user id.
The stored rating content still carries no author reference.

### Logging

Logging follows GDPR data minimization: only what diagnosis needs is recorded, and no entry stored in
Production survives more than 15 days. Never logged: user or personal-row ids, names, emails, phone numbers,
anything typed by the client, the requested path or query, client IP, user agent, cookies, tokens and
one-time codes. Logged: counters, enum/kind values, error/SMTP/HTTP codes, the HTTP method, the matched
endpoint's route template, and exception type/message/stack traces.

- **API**: every event is declared with `[LoggerMessage]` in a `*Log` class per layer — `SecurityLog`,
  `ApplicationLog`, `InfrastructureLog`, `ApiLog`. `CA1848` is an error (`backend/.editorconfig`), and the
  architecture tests `LoggingConventionTests`/`LoggingCallSiteTests` enforce an allowed-placeholder list and
  that `Information` is used only for process lifecycle. `Logging:LogLevel` in `appsettings.json` is the level
  policy for both the console and the file sink: `Default` is `Warning`; `Information` is enabled only for
  `CodigoActivo.Lifecycle` and `Microsoft.Hosting.Lifetime`; `Microsoft.EntityFrameworkCore.Database.Command`
  is `None`, since the SQL of a failing command can carry literal values. An environment can override any
  category with `Logging__LogLevel__<Category>`. There is no per-request logging middleware: unhandled
  exceptions are recorded only by `GlobalExceptionHandler`, with the HTTP method and the route template, never
  the requested path. EF Core sensitive-data logging stays disabled; `SmtpEmailSender` strips the recipient
  and server reply out of SMTP exceptions before they are logged.
- **Security events**: only four events exist, all `Warning` and free of any user id —
  `PasswordLockoutTriggered` and `TwoFactorLockoutTriggered` (an account or its second factor was locked),
  `AdministratorFlagChanged` (the administrator flag changed) and `SecurityNotificationRateLimited` (a
  security-change email was dropped by the limiter). Logins, logouts and password changes are not recorded.
- **API log output**: the flat `LOG_DIRECTORY` variable selects the sink. With a value, every event goes to
  daily files `api-<yyyyMMdd>.log` (Serilog file sink; the file name uses the container's local date, event
  timestamps are UTC), one line per event with embedded line breaks folded with `" | "`, and any GUID in the
  message or the exception (dashed or plain form) masked as `<id>`; nothing goes to the console. Without a
  value (or empty, the local-development case) events go to the console, unmasked. The fatal startup event
  (`CRT CodigoActivo.Lifecycle The API host terminated unexpectedly`) is always recorded regardless of the
  level policy, and the process exits non-zero. If the daily file cannot be opened for writing, the API does
  not start.
- **nginx**: `access_log off` and `error_log` writes to `/dev/stderr` at `crit`, so no request or error is
  stored; a failed API shows in `docker compose ps`, and the 5xx it returns is logged by the API itself.
- **PostgreSQL**: `log_error_verbosity=terse` drops `DETAIL` lines; stderr output is discarded, so nothing is
  stored. A database failure is logged as the exception it raises in the API. A known residual: PostgreSQL
  itself still logs the attempted role name in `FATAL` authentication failures on its discarded stderr; `db`
  is reachable only from the internal `backend` network.
- **Retention and container logs**: every base Compose service uses `logging: driver: none` (`docker compose
  logs` shows nothing). Only the API writes log files, into the `logs-api` volume; `LogFileRetentionCleaner`,
  a background service registered whenever `LOG_DIRECTORY` is set, sweeps that directory at startup and every
  hour and deletes `api-<yyyyMMdd>[_N].log` files whose last write is older than 13 days. Because a daily file
  spans 24 hours, no stored entry survives more than 14 days plus one sweep interval, under the 15-day limit.
  A removal failure is logged as `LogFileRemovalFailed` (`Warning`) and does not stop the sweep; no other file
  in the directory is ever touched. Purging stops while the API is stopped, so the effective retention
  lengthens by however long it is down. See [DEPLOYMENT.md](DEPLOYMENT.md#production-topology).

### Production configuration and secrets

Production startup fails when `POSTGRES_PASSWORD` is under 16 characters, `DATA_PROTECTION_CERTIFICATE_PASSWORD`
is under 32, `APP_BASE_URL` is not a clean public HTTPS origin, SMTP host/port/sender/transport encryption is
invalid, or only one of `SMTP_USERNAME`/`SMTP_PASSWORD` is set.

Secrets are flat environment variables in the ignored root `.env`; `appsettings.json` contains no
credentials. Restrict `.env` to the deployment account and keep it out of images and unencrypted backups.
Rotating the database password must update both the PostgreSQL role and the API configuration. Keep the Data
Protection certificate password separate from the key-volume backup.

### Data Protection and containers

Production persists ASP.NET Data Protection keys in `api-dataprotection`. Every key ring element is stored
as an `urn:codigoactivo:data-protection:aes-gcm:v1` XML element carrying a salt, nonce, authentication tag,
ciphertext and an Ed25519 signature; decryption first verifies that signature against the certificate pinned
in the volume, then derives the AES-256-GCM key from `DATA_PROTECTION_CERTIFICATE_PASSWORD` with Argon2id
(`Argon2idKeyDerivation`: 3 iterations, 64 MiB, 4 lanes, a 16-byte salt — the same parameters and code path
that hash passwords) before decrypting with `System.Security.Cryptography.AesGcm`. The locally generated
Ed25519 certificate's own private key is stored the same way, in a fixed-size binary container (version,
salt, nonce, tag, ciphertext) with the certificate's DER bytes as associated data. BouncyCastle is used only
to generate and parse the Ed25519 certificate and to sign/verify; it performs no encryption. Since the
certificate password derives the key-wrapping key, there is no rotation procedure: changing it means
recreating the volume, which invalidates every session and every stored authenticator secret. Outside
Production the key ring is unencrypted on disk, but every environment protects Data Protection payloads
themselves — session and two-factor cookies, antiforgery tokens, authenticator secrets and the email outbox
content described below — with AES-256-GCM. Application containers run as non-root, drop all capabilities,
enable `no-new-privileges` and use read-only root filesystems; PostgreSQL is reachable only on the internal
backend network. The development override removes parts of this boundary and must not be deployed.

### Files and multipart requests

Stored uploads are limited to 10 MiB by default and saved under `/app/files` in the `api-files` volume; nginx
also enforces a `12m` request-body limit. Access to stored files goes through API authorization, not a direct
static volume mount. Administrator email attachments never reach the `api-files` volume: they are read once
per batch into the Data Protection-protected outbox content described above and removed once every message
referencing them is delivered or discarded, with count, combined bytes and recipient count bounded by
application settings.

## Email abuse controls

New accounts must always confirm an emailed OTP before their first login; there is no switch. Verification
and password-reset codes expire after 15 minutes with a 60-second resend cooldown; login and account-deletion
codes share storage and expire after 10 minutes with the same cooldown. SMTP must be configured everywhere or
the API refuses to start. Activity signup sends no message; confirming or rejecting one queues an outcome
email after commit, always to the guardian address for a dependent minor. Delivery errors never roll back
registration, recovery, activity decisions or security changes.

Changing an account's password (by the user or through recovery), its second factor (authenticator confirmed,
returned to email, or reset by an administrator), its administrator flag or its email or phones queues a
notification to the affected account after commit, from the handler, whoever asked for the change. The notice
names the change and its timestamp and carries no code, secret or link that performs an action; an email or
phone change is announced to the **previous** address and only ever quotes the new one masked. These messages are
ordinary automatic mail, not credential mail, so they spend the shared budget without touching the credential
reserve that login codes rely on.

Every automatic verification, password-reset, second-factor-code, security-change and activity-decision email
passes through `ThrottledEmailSender`: each normalized destination has burst/hourly/daily budgets, the
process has a global budget with a credential-email reserve, and normalization lowercases addresses, strips
sub-address tags and folds dots only for Gmail/Googlemail. Quota is spent on attempt, before the message
reaches the outbox; SMTP failure and a full outbox do not refund it. The limiter is always enabled,
in-memory, resets on restart and is multiplied by the number of API replicas.

Both automatic and administrator-written mail are stored in a PostgreSQL outbox (`email_outbox_messages`)
and delivered in the background by `EmailOutboxProcessor`, so a request that queues email returns once the
row is committed, not once SMTP accepts it: see [DEPLOYMENT.md](DEPLOYMENT.md#email-delivery) for the
delivery schedule, capacity and monitoring. The stored subject, bodies and attachments are protected with
Data Protection (purpose `CodigoActivo.EmailOutbox.v1`); recipient address/name, kind and attachment names
are stored in clear. Recreating the Data Protection key ring makes pending rows unreadable and they are
discarded on their next delivery attempt.

Only administrators may use `/api/emails/...` endpoints; recipients are resolved on the server from the same
filters as user/attendee lists, never client-supplied addresses. Each recipient gets a separate message,
minors without their own address are skipped, and attachments remain transient. This mail bypasses the
automatic-message limiter; the endpoint reports how many messages were queued and how many recipients were
skipped, not what SMTP later did with them. Single-recipient messages allow 30 requests/minute
per administrator (10 executing, 10 waiting); bulk messages allow 5 requests/minute (2 active, no waiting
queue). All requests remain subject to nginx's general API limit, application recipient/attachment limits and
the five-minute nginx upstream timeout.

`GET /api/emails/users/audience` and `GET /api/emails/events/{eventId}/attendees/audience` let an
administrator preview a send before committing to it: given the same filters as `POST /api/emails/users` or
`POST /api/emails/events/{eventId}/attendees`, they return `{ recipients, withoutConsent }` computed by the
same shared selection (`ManualEmailAudience`) the send commands use, so the preview always matches what a
send would reach — distinct addressable recipients deduplicated case-insensitively, and how many of them
have `promotionalConsent` set to `false`. These preview endpoints carry none of the send limits above beyond
nginx's general API limit.

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
