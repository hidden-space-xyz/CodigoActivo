# Deployment

The production distribution is the root `docker-compose.yml`. It pulls released images from GitHub Container
Registry and reads configuration from a sibling `.env` file; it does not require a repository clone.

> [!WARNING]
> Running Compose inside a clone without `-f` also loads `docker-compose.override.yml`, a development overlay
> that builds local code, publishes the database and weakens container isolation. Use
> `docker compose -f docker-compose.yml ...` for production from a clone.

## Production topology

| Service | Image                                                   | Exposure and role                                                                |
| ------- | ------------------------------------------------------- | -------------------------------------------------------------------------------- |
| `db`    | `postgres:18-alpine`                                    | PostgreSQL on the internal `backend` network; no production host port            |
| `api`   | `ghcr.io/hidden-space-xyz/codigoactivo-backend:latest`  | ASP.NET Core on `api:8080`; reachable only by the Compose networks               |
| `web`   | `ghcr.io/hidden-space-xyz/codigoactivo-frontend:latest` | Unprivileged nginx serving the SPA and proxying API/SEO routes; host port `8080` |

The shipped mapping is `8080:8080`, so `web` listens on **all host interfaces** over plain HTTP; put it
behind a TLS-terminating reverse proxy and restrict direct access to `8080` with a firewall or network policy.
Networks: `frontend` (shared by `web` and `api`) and internal-only `backend` (shared by `api` and `db`).

| Volume               | Contents                                                           |
| -------------------- | ------------------------------------------------------------------ |
| `db-data`            | PostgreSQL data                                                    |
| `api-files`          | Uploaded files                                                     |
| `api-dataprotection` | ASP.NET Data Protection keys and the generated Ed25519 certificate |
| `api-state`          | The immutable demo/normal deployment-mode choice                   |
| `logs-api`           | Daily API log files                                                |

Every base service disables container logging (`logging: driver: none`); `docker compose logs` shows nothing.
Only the API writes log files, into `logs-api`, and purges them itself; see [SECURITY.md](SECURITY.md#logging)
for what is logged and how retention works. Do not include `logs-api` in backups: it holds only diagnostics
subject to that retention. Queued email is persisted in `db-data`, not held in memory; see
[Email delivery](#email-delivery).

Both application containers run as non-root, drop Linux capabilities, use `no-new-privileges` and have
read-only root filesystems with explicit writable mounts. Health checks target `/api/auth/csrf` (API) and
`/healthz` (nginx).

nginx gzips text responses of 1 KiB or more on the fly, both static files and proxied API responses; the API
itself does not compress. The `web` image build also stores a `.gz` sibling of each text asset, which nginx
serves directly (`gzip_static`). A reverse proxy in front must not strip `Accept-Encoding`.

### Capacity and rate limits

nginx allows 8,192 connections per worker, 4,000 active client connections and 2,000 per IP (`web`'s open-file
limit is raised to 65,536). Application request and credential-work rate limits are documented once, in
[SECURITY.md](SECURITY.md#passwords-and-credential-endpoints). The shipped budgets baseline one 4-core, 8-GiB
host targeting 1,000 concurrently active users; validate on production-equivalent infrastructure.

## First production start

```bash
curl -LO https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/docker-compose.yml
curl -Lo .env https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/.env.example
# Edit .env and replace all required or placeholder values.
docker compose up -d
docker compose ps
docker compose exec api sh -c 'tail -n 100 /var/log/codigoactivo/api-*.log'
```

Set at least `POSTGRES_PASSWORD` (16+ chars), `DATA_PROTECTION_CERTIFICATE_PASSWORD` (32+ chars),
`APP_BASE_URL` (final public HTTPS origin, no path/query/fragment), `DEMO_MODE` (permanent for these
volumes), `BOOTSTRAP_ADMIN_EMAIL`/`BOOTSTRAP_ADMIN_PASSWORD` (used only when the user table is empty), and
`SMTP_HOST`, `SMTP_PORT`, `SMTP_SECURITY`, `SMTP_FROM_ADDRESS` plus any SMTP credentials.

With `logging: driver: none`, an `api` container that keeps restarting without a new file appearing in
`logs-api` means `LOG_DIRECTORY` is not writable or the startup configuration is invalid; the fatal event
still reaches the file whenever the directory is writable.

Every login is completed with a one-time code, emailed unless the user enrolled an authenticator app, so
startup fails everywhere when `SMTP_HOST` or `SMTP_FROM_ADDRESS` is missing. Production also requires an
encrypted SMTP mode (`StartTls` or `SslOnConnect`) and valid host/port/sender values. `APP_BASE_URL` must use
a public DNS name; IP addresses, localhost and reserved example/test domains are rejected.

### TLS and proxy boundary

The external proxy must terminate HTTPS, accepting only TLS 1.3 with TLS 1.2 as the sole fallback and
disabling TLS 1.1, TLS 1.0 and SSL; overwrite client-supplied forwarding headers; and send the effective
scheme as `X-Forwarded-Proto`. nginx normalizes that header itself before forwarding it to the API: only an
exact `https` value (case-insensitive) counts, so any other value — including a comma-separated list such as
`https, http` — or a missing header becomes `http`. Because nginx only reads the header value, not the peer,
the external proxy must overwrite `X-Forwarded-Proto` rather than append to it; appending makes nginx see
`http`, so it skips HSTS and forwards `http` to the API. The API accepts one forwarded hop, redirects HTTP to
HTTPS in Production, and sets secure `__Host-` cookies; nginx emits HSTS only when its normalized scheme is
HTTPS. The API honours
`X-Forwarded-For`/`X-Forwarded-Proto` only from loopback and private peers (RFC 1918 and `fc00::/7`), so a
proxy or container network outside those ranges makes it ignore both headers, which in Production means an
HTTPS redirect loop and a single shared rate-limit partition for every client. `APP_BASE_URL`
controls links in email and the URLs generated in `/sitemap.xml` and `/robots.txt`; it must match the public
origin.

## Environment variables

The base Compose file forwards an explicit variable list to the API. Adding an arbitrary .NET `SECTION__KEY`
value to `.env` has no effect until the same variable is added under `api.environment` in
`docker-compose.yml`.

| Variable                               | Meaning                                                                            | Template value                                 |
| -------------------------------------- | ---------------------------------------------------------------------------------- | ---------------------------------------------- |
| `POSTGRES_HOST`                        | Database host used by the API                                                      | `db`                                           |
| `POSTGRES_PORT`                        | Database port                                                                      | `5432`                                         |
| `POSTGRES_DB`                          | Database name                                                                      | `codigoactivo`                                 |
| `POSTGRES_USER`                        | Database user                                                                      | `codigoactivo`                                 |
| `POSTGRES_PASSWORD`                    | Database password; 16+ characters in Production                                    | Empty                                          |
| `DATA_PROTECTION_CERTIFICATE_PASSWORD` | Encrypts the Ed25519 private key and the key ring; 32+ characters, not rotatable   | Empty                                          |
| `APP_BASE_URL`                         | Public origin for links and SEO output                                             | `https://example.org` (invalid for Production) |
| `APP_TIMEZONE`                         | IANA or Windows time-zone ID used by the application clock                         | `Europe/Madrid`                                |
| `DEMO_MODE`                            | `true` or `false`; locked on first start                                           | `false`                                        |
| `BOOTSTRAP_ADMIN_EMAIL`                | Initial administrator email for an empty database                                  | Empty                                          |
| `BOOTSTRAP_ADMIN_PASSWORD`             | Initial administrator password, 12–128 characters                                  | Empty                                          |
| `SMTP_HOST`                            | SMTP host; always required because login codes are emailed                         | Empty                                          |
| `SMTP_PORT`                            | SMTP port                                                                          | `587`                                          |
| `SMTP_SECURITY`                        | `StartTls`, `SslOnConnect`, `None` or `Auto`; Production allows only the first two | `StartTls`                                     |
| `SMTP_USERNAME`                        | SMTP username; must be paired with a password                                      | Empty                                          |
| `SMTP_PASSWORD`                        | SMTP password; must be paired with a username                                      | Empty                                          |
| `SMTP_FROM_ADDRESS`                    | Single sender address                                                              | Empty                                          |
| `SMTP_FROM_NAME`                       | Sender display name                                                                | Empty                                          |

Neither the template nor Compose defines `PGDATA`; PostgreSQL uses the default data directory inside the
volume mounted at `/var/lib/postgresql`.

For a direct `dotnet run`, missing database variables fall back to `localhost:5432` and database/user
`codigoactivo` with an empty password; missing `APP_BASE_URL` falls back to `http://localhost:5173`. SMTP has
no fallback: point `SMTP_HOST` at a mail catcher such as the development overlay's Mailpit
(`localhost:1025`, security `None`), where login codes can also be read.

### Application settings

Defaults in `backend/src/CodigoActivo.API/appsettings.json`, overridable with standard .NET environment
syntax (for example `AUTH__EXPIREHOURS`; also add the uppercase variable to the Compose API environment list):

| Setting                                       | Default             |
| --------------------------------------------- | ------------------- |
| `Auth:ExpireHours`                            | `720` (30 days)     |
| `SessionCleanup:IntervalMinutes`              | `60`                |
| `FileStorage:MaxSizeBytes`                    | `10485760` (10 MiB) |
| `AccountVerification:OtpLifetimeMinutes`      | `15`                |
| `AccountVerification:ResendCooldownSeconds`   | `60`                |
| `PasswordReset:CodeLifetimeMinutes`           | `15`                |
| `PasswordReset:ResendCooldownSeconds`         | `60`                |
| `PasswordLockout:MaxFailedAttempts`           | `5`                 |
| `TwoFactor:ChallengeLifetimeMinutes`          | `10`                |
| `TwoFactor:ResendCooldownSeconds`             | `60`                |
| `TwoFactor:AuthenticatorSetupLifetimeMinutes` | `15`                |
| `TwoFactor:MaxFailedAttempts`                 | `5`                 |
| `TwoFactor:LockoutMinutes`                    | `15`                |
| `TwoFactor:Issuer`                            | `Código Activo`     |
| `ManualEmail:MaxRecipients`                   | `500`               |
| `ManualEmail:MaxAttachments`                  | `10`                |
| `ManualEmail:MaxAttachmentsBytes`             | `8388608` (8 MiB)   |

The upload limit is enforced by the API and accompanied by 64 KiB of multipart overhead. nginx separately
limits request bodies to `12m`; raising the application limit may also require changing
`frontend/docker/default.conf`. Manual-email attachments share the API request limit and are never stored in
`api-files`.

## Email delivery

Every email — automatic messages and administrator-written bulk mail alike — is stored in a PostgreSQL
outbox (`email_outbox_messages`, `email_outbox_contents`, `email_outbox_content_parts`) and delivered in the
background by the `EmailOutboxProcessor` hosted service; a request that queues email returns once the row
is committed, not once SMTP accepts it. Automatic messages still pass through a process-local rate limiter
before reaching the outbox; administrator bulk email bypasses that budget but is queued the same way, so its
response (`{ queued, skipped }`) reports what was accepted, not what was delivered.

The guard cannot be disabled and falls back to defaults on invalid settings. Limits are per API process:
restarts refill them, replicas multiply them.

| Setting                              | Purpose                                                                  | Default |
| ------------------------------------ | ------------------------------------------------------------------------ | ------- |
| `EmailGuard:RecipientBurst`          | Immediate messages allowed per normalized address                        | `20`    |
| `EmailGuard:RecipientPerHour`        | Sustained hourly limit per address                                       | `10`    |
| `EmailGuard:RecipientPerDay`         | Daily limit per address                                                  | `50`    |
| `EmailGuard:GlobalBurst`             | Immediate process-wide budget                                            | `1000`  |
| `EmailGuard:GlobalPerHour`           | Sustained process-wide hourly budget                                     | `1000`  |
| `EmailGuard:GlobalCredentialReserve` | Global capacity reserved for verification, reset and second-factor codes | `200`   |
| `EmailGuard:MaxTrackedRecipients`    | Maximum address budgets held in memory                                   | `50000` |
| `EmailGuard:SweepIntervalMinutes`    | Idle-budget cleanup interval                                             | `5`     |
| `EmailGuard:AlertIntervalMinutes`    | Minimum interval between repeated guard alerts                           | `15`    |

Pending mail survives a restart: rows are claimed with `FOR UPDATE SKIP LOCKED` and a lease, in priority
order (2FA/verification/reset codes first, ordinary automatic mail next, administrator bulk mail last), by
a poll loop (`EmailQueue:PollIntervalSeconds`) also woken immediately by same-process enqueues. A message is
attempted up to 5 times, retried after 1 minute, 5 minutes, 30 minutes and 2 hours on the first four
failures; the 5th failure discards the row and logs an error. Delivery is at-least-once: a crash between a
successful send and removing the row can resend it. `EmailQueue:Capacity` only bounds ordinary automatic and
bulk mail; login, verification and password-reset codes are always accepted (and still bounded by the
`ThrottledEmailSender` budgets above). Shutdown only waits for sends already in flight, up to
`EmailQueue:ShutdownDrainSeconds`, which must stay below the API service's 30-second `stop_grace_period`;
whatever is not delivered by then stays in the outbox for the next start. Raising `EmailQueue:BatchSize`
without also raising `EmailQueue:Workers` lengthens the lease held on a claimed batch.

| Setting                           | Purpose                                               | Default |
| --------------------------------- | ------------------------------------------------------ | ------- |
| `EmailQueue:Capacity`             | Approximate cap on pending non-critical messages      | `1000`  |
| `EmailQueue:Workers`              | Concurrent SMTP sends per claimed batch, capped at 16 | `4`     |
| `EmailQueue:BatchSize`            | Due messages claimed at once, capped at 100           | `10`    |
| `EmailQueue:ShutdownDrainSeconds` | Graceful drain time, capped at 300 seconds            | `20`    |
| `EmailQueue:SendTimeoutSeconds`   | Per-message timeout, capped at 600 seconds            | `60`    |
| `EmailQueue:PollIntervalSeconds`  | Wait between poll passes, capped at 300 seconds       | `5`     |

None of the `EmailQueue:*` settings are forwarded by the base Compose file; changing one requires both the
uppercase environment variable and adding it to `api.environment` in `docker-compose.yml`.

Monitor the `InfrastructureLog` events for recipient throttling (`RecipientEmailQuotaReached`), global-budget
pressure (`GlobalEmailBudgetLow`, `GlobalEmailBudgetExhausted`), delivery failures and exhausted attempts
(`EmailDeliveryAttemptFailed`, `EmailDeliveryGaveUp`), and an incomplete shutdown drain
(`EmailOutboxDrainIncomplete`). See [SECURITY.md](SECURITY.md#logging) for where these events are written.

## Locked accounts

Five wrong passwords lock an account for good: the lock has no expiry and is lifted only by a completed
password reset, which needs working SMTP. Anyone who knows an account's email can lock accounts in bulk, so
alert on the `PasswordLockoutTriggered` log event. If every administrator is locked while email is
unavailable, unlock against the `db` container:

```bash
docker compose exec db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c \
  "UPDATE users SET password_failed_attempts = 0, password_locked_at = NULL WHERE email = 'admin@example.org';"
```

## Demo mode and initial administrator

On first start the API writes the selected `DEMO_MODE` value to `/app/state/deployment-mode` in `api-state`;
later starts must use the same value. The file is always written except when the API runs in Development
outside a container (the API image declares `DOTNET_RUNNING_IN_CONTAINER=true`, and `1` is accepted too), so
a local `dotnet run` still validates `DEMO_MODE`, logs that the lock is skipped and writes nothing to the
developer's filesystem, while the Compose development overlay still locks the mode in `api-state`.
Demo mode seeds realistic content and invented accounts under
`demo.codigoactivo.es` with random, discarded passwords, so they cannot log in; the demonstrator signs in with
the bootstrap administrator from `.env`, the only administrator, whose login codes arrive at
`BOOTSTRAP_ADMIN_EMAIL` through the configured SMTP server.

Changing mode requires destroying all named volumes and therefore all application data:

```bash
docker compose down -v
docker compose up -d
```

> [!CAUTION]
> `docker compose down -v` permanently deletes the database, uploads, Data Protection keys and mode lock. Use
> it only for a deliberately disposable environment.

When the user table is empty, startup requires a valid bootstrap email and a 12–128 character password and
creates the first active administrator before accepting requests, seeded with the fixed DNI/NIE `00000000T`;
change it from the administrator's profile after first login, since no other account can register with that
value while it is in use. Once any user exists, both bootstrap variables are ignored.

## Development overlay

Inside the repository, `docker compose up --build` merges `docker-compose.override.yml`: it builds `api` and
`web` from the working tree, sets the API to Development and exposes port `5150`, adds a Mailpit service
publishing SMTP on port `1025` and its inbox UI on port `8025`, exposes PostgreSQL on port `5432` and makes
the backend network non-internal, and disables the API read-only root filesystem in favor of
debugger-oriented privileges. Visual Studio uses the same Compose project through
`backend/docker-compose.dcproj`. As a development-only exception to the log policy in
[SECURITY.md](SECURITY.md#logging), the overlay clears `LOG_DIRECTORY`, so the API logs to the console
unmasked, kept by Docker's `json-file` driver at 5 MB per file, two files.

## Releases and upgrades

Only pushes to `master` start CI (including merged PRs); unmerged PRs run nothing.
Backend build/unit/integration tests and frontend checks must pass before publishing. The Docker workflow
is a reusable CI stage; it does not run independently or on a schedule.

Pushes to `develop` or `master` that change `backend/`, `frontend/` or the workflow itself start the CodeQL
workflow, which is independent of CI and does not gate publishing. A newer push to the same branch cancels
the scan in progress.
Findings never fail the run; review them in the repository's code scanning alerts or in the
`codeql-sarif-<language>` workflow artifacts, retained for 7 days. Only a broken build, analysis or upload fails it.
C# analysis uses a full .NET 10 build under CodeQL tracing, including source generators and test projects;
results located in `obj/` or `bin/` (generated code) are dropped before upload. JavaScript/TypeScript uses
the build-free analysis mode.

API and UI share one version and one GitHub release, tagged `vX.Y.Z`. All repository commits since the
highest stable `vX.Y.Z` tag count. With no matching tag, the baseline is `0.0.0` and the full commit history
is considered.
The largest Conventional Commit increment wins: `feat` = minor, `fix`/`perf` = patch, any type with `!` = major.
`refactor`, `docs`, `test`, `style`, `build`, `ci`, `chore` and non-conventional subjects produce no increment.
Scopes are optional; for squash merges use a conventional PR title. Normal merges also inspect branch commits.
With no qualifying commits, publishing is skipped for both containers.

Each release builds and pushes both GHCR images with the same `X.Y.Z`, even when only one component changed.
Only after both pushes succeed does the final job update their `latest` tags and create one GitHub release
with generated notes and pull commands for both images at the checked commit. Versions are not stored in
project files. Runs are serialized without cancelling an active release; GitHub may replace a pending run
with a newer push, whose commit range still includes unreleased changes. Reruns skip an already released
version; rerunning a commit older than the latest release is rejected. Actions needs permission to write
repository contents, packages and security events.

The production Compose file follows `latest`. Upgrade with `docker compose pull && docker compose up -d`, then
review the daily log files in `logs-api` (see [SECURITY.md](SECURITY.md#logging)) and smoke test. Recreating
the containers this way discards any `json-file` logs Docker kept from an earlier version; the daily files in
`logs-api` are unaffected. PostgreSQL 18 is mounted at `/var/lib/postgresql`, with no in-place upgrade from
older major versions.

## Backups and recovery

Back up `db-data`, `api-files`, `api-dataprotection` and `api-state`, and test restoration regularly. Keep
`DATA_PROTECTION_CERTIFICATE_PASSWORD` separately from the `api-dataprotection` backup; losing either makes
the protected key ring unusable and invalidates sessions. That password also derives the key-wrapping key of
every stored key element, so it cannot be rotated on its own: a new password requires recreating the volume,
which invalidates all sessions and stored authenticator secrets (see
[SECURITY.md](SECURITY.md#data-protection-and-containers)). The email outbox lives in `db-data`, so a
`db-data` backup includes pending mail; restoring an older backup can redeliver or drop messages queued
after it was taken, and its protected content depends on the Data Protection key ring in effect when it was
queued.

Before public exposure, verify TLS and forwarded headers, firewall access to port `8080`, SMTP delivery,
database and volume recovery, registration and email verification, the two-step login, password reset,
authorization changes, file access and the selected demo mode. Review [SECURITY.md](SECURITY.md) for the
complete security model.
