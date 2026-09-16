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
- Public registration never grants administrator access. On an empty database, startup creates the first
  administrator from `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD`; those variables are ignored once
  a user exists.

### CSRF

`CsrfValidationMiddleware` validates every unsafe HTTP method. The client obtains a token from
`GET /api/auth/csrf` and returns it in `X-CSRF-TOKEN`. The SPA client handles this exchange automatically.

### Passwords and credential endpoints

Passwords must contain 12–128 characters and are hashed with Argon2id. Passwords and one-time codes are never
stored or logged in plaintext. Login performs fallback Argon2 work for unknown identifiers to reduce timing
differences.

Credential routes have layered resource controls:

- nginx limits them to 120 requests per minute per client IP, with a burst of 100;
- the API applies the same 120-per-minute production window;
- memory-hard credential work has a process-wide concurrency limit of four and a FIFO queue of 128.

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

When `ACCOUNT_VERIFICATION_REQUIRED=true`, new accounts must confirm an emailed OTP before login. Verification
and password-reset codes expire after 15 minutes by default and have a 60-second resend cooldown.

Activity signup itself sends no message. Confirming or rejecting a signup after the status change commits
queues an outcome email. A dependent minor's notification resolves to the guardian address on the server;
the request cannot choose a destination.

Delivery errors never roll back registration, recovery requests or activity decisions. They are logged by
the background dispatcher.

### Automatic-message limiter

Every automatic verification, password-reset and activity-decision email passes through
`ThrottledEmailSender` before entering the queue.

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
response can report delivered, failed and skipped counts. Protect administrator accounts accordingly. These
requests remain subject to nginx's general API limit, application recipient/attachment limits and the
five-minute nginx upstream timeout.

## Demo mode

`DEMO_MODE=true` creates accounts with the public password `Demo1234!`. Such deployments are disposable
demonstrations and must never contain private data. The first selected mode is persisted in `api-state`; a
later conflicting value stops startup. Changing it requires deleting all named volumes as described in
[DEPLOYMENT.md](DEPLOYMENT.md#demo-mode-and-initial-administrator).

## Operational checklist

- Keep the TLS proxy as the only public ingress and restrict direct port `8080` access.
- Back up and test restoration of the database, uploads, mode state and Data Protection material.
- Alert on authentication abuse, email-budget exhaustion, queue saturation and SMTP delivery failures.
- Review administrator access and smoke-test session revocation, file authorization and account recovery
  after releases.
- Treat the development override, demo credentials and any copied production `.env` as sensitive operational
  risks.
