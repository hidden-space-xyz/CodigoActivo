# Security Policy

## Reporting a vulnerability

> [!IMPORTANT]
> Please report security issues **privately** — do not open a public GitHub issue.

- **Preferred:** use GitHub's *"Report a vulnerability"* on the repository's **Security → Advisories** tab.

Please include steps to reproduce and the affected version/commit. We will acknowledge your report as soon
as possible and keep you updated on the fix. Only the latest deployed version (tracking the `master` branch)
is supported.

## Security model

### Authentication & authorization

- Auth is a **session cookie** (`HttpOnly`; `Secure` in Production; `SameSite` from `AUTH_SAMESITE`, default
  `Lax`; sliding 8-hour expiry). There is no JWT and no cross-origin token — the whole app is same-origin.
- Authorization is a **boolean admin flag** (an `isAdmin` claim), not roles. Endpoints are guarded by the
  custom attributes `[AllowOnlyAdmin]` and `[AllowOnlySelf]` (self = the target user *or* their dependent
  child). `UserType`/`UserStatusType` are domain lookups, not authorization roles.

> [!WARNING]
> The **first user ever registered is auto-promoted to admin**. Create that account yourself before exposing
> the site publicly — otherwise anyone who registers first gains admin.

### CSRF protection

Antiforgery is enforced for **all unsafe HTTP methods** by `CsrfValidationMiddleware`. Clients fetch a token
from `GET /api/auth/csrf` and send it as the `X-CSRF-TOKEN` header; the SPA's HTTP client does this
transparently.

### Passwords

Passwords are hashed with **Argon2id** (Konscious.Security.Cryptography). Plaintext passwords are never
stored or logged.

### Input validation & error handling

Requests are validated with DataAnnotations plus custom attributes. Every failure is returned in one uniform
shape — `ApiErrorResponse(Title, Status, Code, TraceId)` with a string `ErrorCode` — so stack traces and
internal details never leak to clients. A `TraceId`/correlation id ties a client error back to the
server logs.

### Transport security

The API enables forwarded headers and, in Production, redirects HTTP → HTTPS and marks cookies `Secure`.
Deploy it behind a TLS-terminating reverse proxy that sets `X-Forwarded-Proto` — see
[DEPLOYMENT.md](DEPLOYMENT.md#tls--reverse-proxy).

### Secrets management

All secrets are supplied as **flat environment variables**, kept in the git-ignored root `.env` (template:
`.env.example`). Nothing sensitive is committed: there are no `dotnet user-secrets` and no credentials in
`appsettings.json`. Rotate `POSTGRES_PASSWORD` and SMTP credentials as usual by updating `.env` and
redeploying.

### Container hardening

The production Compose stack (`docker-compose.yml`) runs with:

- the **database on an internal-only network** (never published to the host in production);
- the **API as a non-root user** (uid 1654) with a **read-only filesystem**, **all Linux capabilities
  dropped**, and `no-new-privileges`;
- the **web (nginx) container** running unprivileged; and
- **Data Protection keys** persisted to the `api-dataprotection` volume so cookies survive restarts.

> [!WARNING]
> The `docker-compose.override.yml` development overlay deliberately relaxes this hardening and exposes the
> database. Never deploy with it merged — always run production with `-f docker-compose.yml`.

### File uploads

Uploads are size-limited (10 MiB by default, `FileStorage:MaxSizeBytes`) and stored under
`FILE_STORAGE_ROOT`. Email attachments are the one multipart path that is **not** stored: they are streamed,
attached to the outgoing message and discarded — they never get a row, a file on disk or a content URL.

### Optional email verification

When `ACCOUNT_VERIFICATION_REQUIRED=true`, new accounts must confirm an emailed one-time code (OTP) before
they can log in; the OTP lifetime and resend cooldown are configurable. Enabling verification requires a
configured SMTP server (`SMTP_HOST` + `SMTP_FROM_ADDRESS`), or the API refuses to start.

### Automatic signup notifications

Signing up for an activity sends no email; the only automatic notification is the outcome an admin emails by
confirming or rejecting the request. It is queued by `ActivitySignupNotifier` *after* the status change has
been committed and delivered by the background dispatcher described below, so no request waits on SMTP.

- **The address never comes from the request.** It is resolved server-side from the enrolled account, and a
  dependent minor — who has no address of their own — resolves to their guardian. A caller cannot redirect a
  notification anywhere.
- **Delivery failures are logged and swallowed.** An SMTP outage must not fail or roll back a status change
  that is already persisted, and the SMTP error must never reach an API response. With `SMTP_HOST` unset the
  status change still succeeds; only the notification is lost (and logged as an error).
- **Every interpolated value is HTML-encoded** into the same branded template the other flows use, so a
  member's name or an activity title cannot inject markup into the outgoing mail.
Send volume is bounded by the outbound email guard described next — flipping a decision back and forth
re-sends the outcome every time, but it stops producing mail once the recipient's budget is spent.

### The outbound email guard

Every **automatic** send — account verification, password reset and the signup decision notification — passes
through one guard before it reaches the SMTP relay, so a new email flow is rate-limited by construction
rather than by remembering to add a cooldown. Admin-written email is exempt (see *Admin-sent email*).

`ThrottledEmailSender` (`backend/src/CodigoActivo.Infrastructure/Communication/`) sits in front of the
background dispatcher and consults `EmailSendLimiter`, two tiers of token bucket held in memory behind one
lock. **The decision is made synchronously, on the request thread, before the message is queued** — which is
what keeps every guarantee below true even though delivery itself now happens in the background:

- **Per destination address** — `EmailGuard:RecipientBurst` (20) immediately, then `RecipientPerHour` (10)
  and `RecipientPerDay` (50). This is the anti-harassment tier.
- **Process-wide** — `GlobalBurst`/`GlobalPerHour` (1000) over all automatic mail, protecting the relay
  account from an address-spray the per-address tier cannot see, with `GlobalCredentialReserve` (200)
  usable only by verification and password reset. Without that reserve an activity-notification flood would
  silently take account recovery down with it.

Three properties are deliberate:

- **The key is the destination address, not the user id**, because `PUT /api/users/{userId}` repoints an
  account's address with no re-verification — a user-id key would be attacker-remappable. The key is
  lowercased, sub-addressing is folded (`victim+1@`, `victim+2@` … share one budget) and dots are folded for
  `gmail.com`/`googlemail.com` only, where they are provably insignificant. Dots stay significant elsewhere.
- **Quota is spent on attempt, not on success.** Both older cooldowns arm only after a send succeeds, so they
  disarm exactly when the relay degrades — the state a flood induces. This layer must not inherit that.
  Queuing does not weaken it: the token is taken before the message enters the queue, so a relay outage
  cannot buy free attempts, and a **full queue is treated exactly like a denial** — the quota stays spent and
  every caller's existing `catch` handles it unchanged.
- **A denial never fails the write, and never leaks.** `ForgotPasswordAsync` still returns its unconditional
  success (the anti-enumeration invariant); signup decisions still commit; registration still returns 201
  without disarming the account's resend cooldown. The single flow that reports anything is
  `POST /api/auth/{userId}/resend-verification`, which answers the existing `409 OtpResendCooldownActive` —
  indistinguishable from that account's own cooldown, so no new oracle is added and no API contract changes.

**The guard cannot be turned off.** There is no kill switch, no env var and no configuration value that
bypasses it: `ThrottledEmailSender` consults the limiter on every call, and it is the only implementation of
the abstraction every automatic flow injects. The `EmailGuard:*` keys tune the numbers, and each one falls
back to its shipped default when the value is missing, zero, negative or unparseable — so a malformed or
hostile config cannot neuter the limits either.

Operationally: the guard logs one line at startup naming every cap, a `Warning` the first time a recipient
starts being held (not once per dropped message), a `Warning` at 20% of the remaining global budget and an
`Error` when it is exhausted. State is **in-memory and per-process** — a restart refills every bucket, and
running more than one `api` replica multiplies every cap by the replica count. Compose defines exactly one.

### The background email dispatcher

Every message the guard admits is handed to `ChannelEmailDispatcher`, a bounded in-memory `Channel` drained by
a small fixed pool of `IHostedService` workers (`EmailQueue:Workers`, 4), so an SMTP round trip never happens
inside an HTTP request. The pool size is a deliberate cap on concurrent connections to the relay — previously
that number was whatever request concurrency happened to be — and it also bounds queue latency, which matters
because verification and reset codes expire 15 minutes after the request rather than after delivery.
Admin-written mail is the deliberate exception: it keeps calling `IEmailTransport` directly, because its
response reports how many messages were delivered and how many failed.

- **Nothing is persisted.** Message bodies — including live OTP codes and password-reset links — exist only in
  process memory, exactly as before; the queue adds no at-rest exposure and no new backup surface.
- **The queue is bounded** (`EmailQueue:Capacity`, 1000, matching `EmailGuard:GlobalBurst`) so a relay outage
  cannot grow it without limit. A full queue logs an `Error` and is reported to the caller as a guard denial.
- **Pending mail is volatile**, like the guard's buckets. On shutdown the worker stops accepting writes and
  drains what it holds within `EmailQueue:ShutdownDrainSeconds` (20 s, under the `api` service's 30 s
  `stop_grace_period`); anything still queued after that — or held when the process is killed outright — is
  logged as a `Warning` and lost. Each individual send is bounded by `EmailQueue:SendTimeoutSeconds` (60 s) so
  one hung connection cannot wedge a worker. All three numbers are clamped at the top as well as the bottom,
  so a hostile or fat-fingered value cannot turn the queue into a silent black hole.
- **Delivery failures are logged and swallowed**, one message at a time; a bad recipient can never stop the
  queue. There is no retry, which is what keeps the guard's accounting honest — a message costs exactly one
  token, once. The operational consequence is that a misconfigured `SMTP_*` no longer shows up as a failing
  request; it shows up as repeated delivery errors from the worker.

nginx's two `limit_req` zones stay in place. They absorb floods before the API is reached and are the only
control over requests that never produce an email; the guard counts messages, which is what protects the relay.

### Admin-sent email

Admins can write a message to a single member or to everyone matching the filters currently applied in the
users table or an event's attendee list. The endpoints (`POST /api/emails/...`) are `[AllowOnlyAdmin]`, so
this capability is one more reason to heed the first-user-becomes-admin warning above.

- **Recipients are resolved server-side** from the same filter objects the list endpoints take. The client
  never supplies addresses, so an admin cannot mail someone the filters do not select.
- **Nobody sees anybody else.** Each recipient gets their own message with a single `To:` — there is no
  `Cc`/`Bcc` list. The messages share one SMTP connection, not one envelope.
- **Dependent minors are skipped**, because they have no address of their own; the response reports how many
  were left out, and the UI hides the per-row button for them.
- **The body is plain text**, HTML-encoded into the branded template — an admin cannot inject markup or
  scripts into the outgoing mail.
- **Attachments are transient** (see *File uploads*), bounded by `ManualEmail:MaxAttachments` and
  `ManualEmail:MaxAttachmentsBytes`, and a single send is capped at `ManualEmail:MaxRecipients`.
- **They are exempt from the outbound email guard, at any volume**, and consume none of its budget — a
  500-recipient send cannot starve account verification or password reset, and can be repeated immediately.
  The exemption is a compile-time property, not a convention: `IEmailSender` (one `SendAsync`) is the guarded
  abstraction and `ThrottledEmailSender` is its only implementation, while the raw `IEmailTransport` is
  injected by exactly two types — `ManualEmailDispatcher` and the dispatcher's own drain loop. `SmtpEmailSender` does
  not implement `IEmailSender` at all, so registering an unguarded sender does not compile.
  `EmailSenderWiringTests` fails if a third type ever takes `IEmailTransport`, and a second assertion pins the
  link below it: `ThrottledEmailSender` is the only type allowed to inject `IEmailDispatcher`, so nothing can
  reach the queue without passing the guard first.
- **The exemption keys on the API surface, never on the caller.** `PATCH /api/activities/{activityId}/{userId}/change-status`
  is also `[AllowOnlyAdmin]`, yet its decision email is fully guarded, because the recipient is a member the
  admin picked — alternating *Confirmed*/*Denied* would otherwise be a mailbomb with admin credentials.
- Each send is logged with its recipient count and delivered/failed/skipped tallies; nothing else records it.
- These endpoints fall under nginx's general `/api` rate-limit zone, **not** the strict credential zone.

### Demo mode

> [!CAUTION]
> `DEMO_MODE` seeds demo accounts with a **well-known password** (`Demo1234!`). It is off by default and
> must never be enabled in a real deployment — see [DEPLOYMENT.md](DEPLOYMENT.md#demo-mode).
