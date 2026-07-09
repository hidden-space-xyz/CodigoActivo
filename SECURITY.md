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
`FILE_STORAGE_ROOT`.

### Optional email verification

When `ACCOUNT_VERIFICATION_REQUIRED=true`, new accounts must confirm an emailed one-time code (OTP) before
they can log in; the OTP lifetime and resend cooldown are configurable. Enabling verification requires a
configured SMTP server (`SMTP_HOST` + `SMTP_FROM_ADDRESS`), or the API refuses to start.

### Demo mode

> [!CAUTION]
> `DEMO_MODE` seeds demo accounts with a **well-known password** (`Demo1234!`). It is off by default and
> must never be enabled in a real deployment — see [DEPLOYMENT.md](DEPLOYMENT.md#demo-mode).
