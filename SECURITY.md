# Security Policy

## Reporting a vulnerability

Report security issues privately through **Security → Advisories → Report a vulnerability** in the GitHub
repository. Do not open a public issue.

Include the affected version or commit, impact, reproduction steps and any suggested mitigation. The project
supports the latest deployed version from `master`.

## Security model

The application assumes a same-origin deployment behind a trusted TLS reverse proxy. The browser reaches
nginx, and nginx serves the SPA and forwards `/api` to the private API container. Cross-origin API use and JWT
authentication are not supported.

### Authentication and authorization

- Authentication uses an ASP.NET Core session cookie. It is `HttpOnly`, `SameSite=Lax`, non-sliding and
  expires after eight hours by default. In Production it is `Secure` and uses a `__Host-` name.
- Every authenticated request revalidates the account status, password fingerprint and administrator flag
  against the database. Blocking or demoting a user takes effect on existing sessions; changing the password
  invalidates them.
- Authorization is a boolean administrator flag, not a role system. `[AllowOnlyAdmin]` protects
  administration endpoints; `[AllowOnlySelf]` accepts the target user or that user's guardian relationship.
  Catalog values such as `UserType` are not authorization roles.
- Granting the administrator flag requires the acting administrator to re-enter their password, so a stolen
  session cookie alone cannot promote another account. A missing or wrong password returns
  `UserCurrentPasswordIncorrect` and changes nothing. Revoking the flag needs no password, but the last
  administrator cannot be demoted.
- Public registration never grants administrator access. On an empty database, startup creates the first
  administrator from `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD`; those variables are ignored once
  a user exists.

### Two-factor authentication

Every account logs in in two steps; there is no opt-out. `POST /api/auth/login` verifies the password and,
instead of a session, issues a separate short-lived challenge cookie (`__Host-CodigoActivo.TwoFactor` in
Production, 10 minutes, non-sliding) whose only use is the second step. It is bound to the user's password
fingerprint and account status, so a password change or a block invalidates it. The session cookie is only
issued by `POST /api/auth/login/two-factor` after the second factor is accepted, which is also when the login
timestamp is recorded.

Each user chooses one second factor from their account:

- **Email (default)**: a 6-digit code is emailed on the password step. It is stored hashed with Argon2id,
  expires with the challenge, is single use, and a new one replaces it. The password step reuses a code sent
  within the last minute instead of sending another, and `POST /api/auth/login/two-factor/resend` enforces
  the same 60-second cooldown. These emails count as credential mail in the automatic-message limiter.
- **Authenticator application**: RFC 6238 TOTP (HMAC-SHA1, six digits, 30-second steps, one step of clock
  drift) delegated to the Otp.NET library; the project implements no cryptographic algorithm itself, and
  secrets, codes and hashes come from Otp.NET, ASP.NET Data Protection, `RandomNumberGenerator` and
  Argon2id. Enrollment starts with the user's password, stores the shared secret encrypted with ASP.NET
  Data Protection (a database copy alone cannot produce codes), and only activates once the application's
  first code is confirmed; an unconfirmed enrollment expires after 15 minutes. Each accepted time step is
  remembered so a code cannot be replayed. Returning to email requires the password and a current code, so
  neither a stolen session nor a stolen password can weaken the second factor on its own.

Wrong codes are counted per account across logins and authenticator removal: after five failures the second
factor is locked for 15 minutes (`TwoFactorLocked`), which makes the 6-digit spaces unguessable within the
per-IP credential rate limits. A successful code resets the counter. Enrollment URIs and shared keys are only
returned to the authenticated owner over HTTPS and never logged.

Account deletion: erasing one's own account from the account panel requires the current password **and**
the second factor on `POST /api/me/deletion` — an emailed 6-digit code, or the authenticator code for users
enrolled in an application. Email users request the code with their password on `POST /api/me/deletion/code`;
it is stored in the same fields as the login code and therefore shares its storage, 10-minute lifetime,
60-second resend cooldown and lockout counter, and the route is refused (`TwoFactorResendNotAllowed`) for
authenticator users, who read their code from the application. Both routes are credential routes. A
successful deletion signs the caller out of the session and challenge schemes and removes, through database
cascade, the user, every minor under their guardianship and all of their participation rows (activity
assignments, event rating submissions and terms acceptances). Past event ratings are not removed: their content
carries no user reference, so they are not personal data of the deleted account, though in an event with very
few ratings it can still be attributable by context (see
[Event rating anonymity](#event-rating-anonymity)). It is refused with `UserDeleteAuthoredContentExists`
while published content still credits the household as author, uploader or last editor, and administrators
cannot delete themselves at all (`UserDeleteAdminForbidden`); they must be demoted first.
`DELETE /api/users/{id}` therefore refuses the caller's own identifier
(`UserSelfDeleteRequiresVerification`) and stays available only for an administrator removing somebody else
and a guardian removing one of their minors.

Recovery: an administrator can reset a user's second factor to email (`POST /api/users/{id}/two-factor/reset`)
after re-entering their own password; this also removes the authenticator and any lockout. Losing the Data
Protection keys makes stored authenticator secrets unreadable, so back them up (see below); affected users
need the same administrator reset.

### CSRF

`CsrfValidationMiddleware` validates every unsafe HTTP method. The client obtains a token from
`GET /api/auth/csrf` and returns it in `X-CSRF-TOKEN`. The SPA client handles this exchange automatically.

### Passwords and credential endpoints

Passwords must contain 12–128 characters and are hashed with Argon2id. Passwords and one-time codes are never
stored or logged in plaintext. Login performs fallback Argon2 work for unknown identifiers to reduce timing
differences.

Credential routes (both login steps and the code resend, registration, verification, password recovery and
change, authenticator enrollment and removal, administrator grants, second-factor resets and the two
self-service account-deletion steps) have layered resource controls:

- nginx rejects credential floods above 10 requests per second per client IP, with a burst of 100;
- the API enforces 120 requests per minute per client IP in every environment;
- memory-hard credential work has a process-wide concurrency limit of four and a short FIFO queue of 16.

Normal API traffic uses different limits because an IP may represent many legitimate users. nginx provides
an outer flood boundary of 200 requests per second per IP with a burst of 500. In every environment, after
authentication, the API applies a sliding one-minute budget of 300 requests per user, or 3,000 requests per
IP for anonymous traffic. At most 128 API requests execute concurrently and another 256 may wait briefly.
Database-intensive report routes have an additional 30-request-per-minute budget per authenticated user,
with 16 executing and 32 waiting process-wide. Multipart file writes allow 30 requests per minute per user,
with 12 executing and 12 waiting process-wide. Rejected API requests include `Retry-After`.

These are per-instance controls. Scaling API replicas multiplies their aggregate capacity.

### Input, output and error handling

- DataAnnotations and custom attributes validate request models.
- Expected failures use a string `ErrorCode`; all failures return
  `ApiErrorResponse(Title, Status, Code, TraceId)` without stack traces or internal exception details.
- User-supplied links allow only absolute HTTP(S) URLs without embedded credentials.
- Rich-text JSON is checked against node, mark, attribute, nesting and size allowlists. Embedded images may
  reference only this application's UUID-based file endpoint.
- User-controlled values inserted into email templates are HTML-encoded. Administrator-authored bodies are
  plain text rendered inside the branded template.

### Transport and proxy trust

In Production the API accepts one forwarded hop, redirects HTTP to HTTPS and emits secure cookies. The base
Compose file keeps `api` and `db` off host ports, but publishes nginx as `8080:8080` on **all interfaces**.

The operator must:

1. terminate TLS in an external reverse proxy;
2. overwrite untrusted `X-Forwarded-For` and `X-Forwarded-Proto` values;
3. prevent untrusted clients from bypassing that proxy and reaching port `8080` directly.

nginx sends HSTS only when the effective forwarded scheme is HTTPS and adds CSP, frame denial, MIME sniffing,
referrer, permissions and cross-origin isolation headers. See
[DEPLOYMENT.md](DEPLOYMENT.md#tls-and-proxy-boundary).

### Privacy and third parties

- Browsers load nothing from third parties. Fonts are bundled from Fontsource packages and served from the
  application origin; the CSP allows scripts, styles, fonts, images and connections only from `'self'` (plus
  `data:`/`blob:` images). Do not add CDNs, analytics or embeds without a legal basis and consent review.
- The only cookies are the session, two-factor challenge and CSRF cookies described above; the theme choice
  stays in `localStorage` and is never sent to the server.
- `Referrer-Policy: same-origin` keeps page URLs out of requests to external sites.
- Verification and password-reset links put the user id and one-time code in the URL fragment
  (`/reset-password#userId=…&code=…`), which browsers never send to the server. The page reads it and removes
  it from the address bar and history entry, so a reload needs the emailed link again.

### Event rating anonymity

Event ratings use two tables with no relationship between them. `event_rating_submissions` records only who
has rated an event (a composite `event_id`/`user_id` primary key that cascades with the event or the user);
`event_ratings` stores only the answer content (score, most liked, least liked, suggestions) under a random
v4 `Guid`, with no user id and no timestamps. `POST /api/events/{eventId}/rating` writes both rows in the same
transaction and cannot edit an existing answer: a second submission for the same event and user is rejected
(`EventRatingAlreadySubmitted`), including under a race, because the composite primary key allows only one
submission per event and user and its violation is translated to that error. The administrator listing
(`GET /api/events/{id}/ratings`) and the member history (whose `hasRated` flag replaces any per-user rating
reference) never expose who authored an answer.

Deleting an account removes the caller's submission rows through cascade but keeps their past ratings, because
the rating content carries no author reference and is therefore not personal data of the deleted account. The
`AnonymizeEventRatings` migration retrofitted this design onto data that used to link ratings to their author:
it copies the existing `event_ratings.user_id` values into the new submission table in a random row order,
drops `user_id`, `created_at` and `updated_at` from `event_ratings`, deletes and reinserts every row of both
tables in a random order so they stop sharing their original transaction identifiers, and runs `CLUSTER` on
both tables so their physical row order no longer matches the pre-migration insertion order (`DROP COLUMN`
alone does not rewrite the table). Its `Down` migration throws instead of running: the link between a rating
and its author was intentionally discarded, not merely hidden, so there is no data left to recreate the old
`user_id`, `created_at` and `updated_at` columns with.

`EventRatingRepository.SubmitAsync` keeps this guarantee for every submission made after the migration, not
only for the rows that existed when it ran: once it inserts a new rating and its submission marker inside its
own transaction, it deletes and reinserts every `event_ratings` and `event_rating_submissions` row for that
event in a random order, so all of that event's rows end up sharing one transaction identifier ("xmin") and an
unrelated physical row order. This holds up against the API, the administration UI, ordinary relational
queries, a raw heap scan keyed on `xmin` or physical row order, and a single query, dump or backup taken after
a submission commits. It does not defend against someone with direct PostgreSQL access to write-ahead log
segments (the `pg_wal` files record every write, including ones this rewrite later hides at the table level,
and travel inside any physical copy of the `db-data` volume, including one taken for point-in-time recovery),
dead tuples left behind until the next `VACUUM`, PostgreSQL statement logs if `log_statement` is enabled, or a
`db-data` backup or logical dump taken **before** the migration ran, which still contains the original author,
creation and update values; rotate such backups out of retention or purge them, see
[DEPLOYMENT.md](DEPLOYMENT.md#backups-and-recovery). Correlating HTTP request logs only narrows a rating to a
client IP address, time and user agent, because neither nginx nor the API's `RequestLoggingMiddleware` records
the authenticated user (see [Logging](#logging)); it cannot point to a registered identity. Because
`SubmitAsync` locks the event row and rewrites every one of that event's rows in both tables on each
submission, its cost grows with the number of existing ratings for that event and each rewrite leaves dead
tuples behind until the next `VACUUM`.

**Known limitations:**

- This anonymity holds against a single, point-in-time observation of the database (an ordinary query, or one
  dump or backup taken after a submission commits). It does not hold against repeated or differential
  observations: comparing two dumps, backups or replica snapshots taken before and after a submission, or
  polling the same tables over time, lets someone see that event's new submission row and new rating row
  appear together and pair them.
- Anonymity depends on there being no stored link between a rating and its author, not on the size of the
  group sharing an event. In an event with only one or two confirmed ratings, the rating count, the listing
  itself or a free-text answer can still let someone deduce authorship from context. Handle results from very
  small groups with care, and consider adding a minimum-rating threshold before surfacing opinions; that
  threshold does not exist today.

### Logging

Every log line must be useful for diagnosis without exposing users or weakening the system:

- **Never logged:** names, email addresses, phone numbers, birth dates, passwords or their hashes, one-time
  codes, TOTP secrets, cookies, CSRF tokens, request bodies, query strings, Referer and SMTP replies to
  recipient or message commands (they quote the mailbox).
- **Logged:** HTTP method, route path (whose parameters are GUIDs), status, timing, entity identifiers, email
  kind, counts, SMTP error and status codes, and exception messages and stack traces.
- **API:** `RequestLoggingMiddleware` records method, path, status and duration only for 4xx/5xx responses.
  Only the `CodigoActivo`, `Program` and `Microsoft.Hosting.Lifetime` categories log at `Information`; every
  other category, including ASP.NET Core, EF Core, Npgsql and `System.Net.Http`, logs at `Warning`, so framework
  request lines (which include query strings) are off. EF Core sensitive-data logging stays disabled, so failed
  commands show parameters as `?`, and Npgsql redacts PostgreSQL error details. `SmtpEmailSender` replaces a
  rejected command's exception with one that carries only the SMTP codes, so email failures log the message
  kind and codes, never the recipient or the server reply that quotes it.
- **nginx:** the access log keeps client IP, time, method, path without query, status, size, timings,
  upstream status, rate-limit outcome and user agent; the Referer is not logged. The error log is limited to
  `crit` because lower levels repeat the full request line and Referer; 429, 502 and 504 outcomes remain in
  the access log.
- **PostgreSQL:** `log_error_verbosity=terse` drops `DETAIL` lines such as
  `Key (email)=(…) already exists`. Failing statements are logged with `$n` placeholders because the API only
  sends parameterized SQL.
- **Retention:** container logs are rotated (see [DEPLOYMENT.md](DEPLOYMENT.md#production-topology)). IP
  addresses and user agents are kept only for abuse and incident diagnosis within that window.

### Production configuration and secrets

Production startup fails when:

- `POSTGRES_PASSWORD` has fewer than 16 characters;
- `DATA_PROTECTION_CERTIFICATE_PASSWORD` has fewer than 32 characters;
- `APP_BASE_URL` is not a clean public HTTPS origin;
- SMTP host, port, sender or transport encryption is invalid;
- only one of `SMTP_USERNAME` and `SMTP_PASSWORD` is provided.

Secrets are flat environment variables in the ignored root `.env`; `appsettings.json` contains no
credentials. Restrict `.env` to the deployment account and keep it out of images and unencrypted backups.
Database password rotation must update both the PostgreSQL role and the API configuration. Keep the Data
Protection certificate password separate from the key-volume backup.

### Data Protection and containers

Production persists ASP.NET Data Protection keys in `api-dataprotection`. Keys are encrypted and signed with
a locally generated Ed25519 certificate whose private key is protected by
`DATA_PROTECTION_CERTIFICATE_PASSWORD`.

The production stack runs application containers as non-root, drops all capabilities, enables
`no-new-privileges` and uses read-only root filesystems. PostgreSQL is reachable only on the internal backend
network. The development override intentionally removes parts of this boundary and must not be deployed.

### Files and multipart requests

Stored uploads are limited to 10 MiB by default and saved under `/app/files` in the `api-files` volume. nginx
also enforces a `12m` request-body limit. Access to stored files goes through API authorization rather than a
direct static volume mount.

Administrator email attachments are transient: they are read into the outbound message and discarded. They
do not create database rows, stored files or public URLs. Attachment count, combined bytes and recipient
count are bounded by application settings.

## Email abuse controls

### Verification, reset and activity notifications

New accounts must always confirm an emailed OTP before their first login; there is no switch. Verification
and password-reset codes expire after 15 minutes by default and have a 60-second resend cooldown. Login
codes, and the account-deletion codes that share their storage, expire after 10 minutes with the same
cooldown. Because every login of an email-method user sends a message, SMTP must be configured in every
environment; the API refuses to start otherwise.

Activity signup itself sends no message. Confirming or rejecting a signup after the status change commits
queues an outcome email. A dependent minor's notification resolves to the guardian address on the server;
the request cannot choose a destination.

Delivery errors never roll back registration, recovery requests or activity decisions. They are logged by
the background dispatcher.

### Automatic-message limiter

Every automatic verification, password-reset, second-factor-code (login and account deletion) and
activity-decision email passes through `ThrottledEmailSender` before entering the queue.

- Each normalized destination has burst, hourly and daily budgets.
- The process has a global budget with capacity reserved for credential email.
- Address normalization lowercases addresses, removes sub-address tags, and folds dots only for Gmail and
  Googlemail domains.
- Quota is spent on an attempt before queueing. SMTP failure and a full queue do not refund it.
- A denial does not expose a new account-enumeration signal or undo the underlying write.

The limiter is always enabled. Its state is in memory, resets on restart and is multiplied by the number of
API replicas.

### Background dispatcher

Accepted automatic mail enters a bounded in-memory channel drained by a fixed worker pool. The queue has no
retry and no persistence. Graceful shutdown attempts to drain it within 20 seconds by default; forced stops
or a longer backlog lose remaining messages. Each send has a 60-second default timeout.

SMTP failures are logged per message and do not stop other workers. A broken SMTP configuration therefore
appears in dispatcher logs rather than as a failed user request. Operational settings and log signals are in
[DEPLOYMENT.md](DEPLOYMENT.md#email-delivery).

### Administrator-authored email

Only administrators may use `/api/emails/...` endpoints. Recipients are resolved on the server from the same
filters used by user and attendee lists; the client never supplies arbitrary addresses. Each recipient gets a
separate message, dependent minors without their own address are skipped, and attachments remain transient.

Administrator mail deliberately bypasses the automatic-message limiter and is delivered synchronously so the
response can report delivered, failed and skipped counts. Protect administrator accounts accordingly.
Single-recipient messages allow 30 requests per minute per administrator, with ten active and ten waiting
process-wide. Bulk messages allow five requests per minute per administrator and two active dispatches
without a waiting queue. All requests remain subject to nginx's general API limit, application
recipient/attachment limits and the five-minute nginx upstream timeout.

## Demo mode

`DEMO_MODE=true` seeds invented accounts that nobody is meant to log in as: each seeding run gives them a
random 32-byte password that is hashed and immediately discarded, they still require the second factor, and
their addresses are fictitious. Demonstrations use the bootstrap administrator configured in Compose. Such
deployments are disposable and must never contain private data. The first selected mode is persisted in
`api-state`; a later conflicting value stops startup. Changing it requires deleting all named volumes as described in
[DEPLOYMENT.md](DEPLOYMENT.md#demo-mode-and-initial-administrator).

## Operational checklist

- Keep the TLS proxy as the only public ingress and restrict direct port `8080` access.
- Back up and test restoration of the database, uploads, mode state and Data Protection material; the
  latter also encrypts stored authenticator secrets.
- Alert on authentication abuse, email-budget exhaustion, queue saturation and SMTP delivery failures.
- Review administrator access and smoke-test session revocation, file authorization and account recovery
  after releases.
- Treat the development override, the bootstrap credentials of demo deployments and any copied production
  `.env` as sensitive operational risks.
