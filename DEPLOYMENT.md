# Deployment

The production distribution is the root `docker-compose.yml`. It pulls released images from GitHub
Container Registry and reads configuration from a sibling `.env` file. It does not require a repository
clone.

> [!WARNING]
> When Compose runs inside a clone without `-f`, it also loads `docker-compose.override.yml`. That file is a
> development overlay: it builds local code, publishes the database and weakens container isolation. Use
> `docker compose -f docker-compose.yml ...` for production from a clone.

## Production topology

| Service | Image                                                   | Exposure and role                                                                |
| ------- | ------------------------------------------------------- | -------------------------------------------------------------------------------- |
| `db`    | `postgres:18-alpine`                                    | PostgreSQL on the internal `backend` network; no production host port            |
| `api`   | `ghcr.io/hidden-space-xyz/codigoactivo-backend:latest`  | ASP.NET Core on `api:8080`; reachable only by the Compose networks               |
| `web`   | `ghcr.io/hidden-space-xyz/codigoactivo-frontend:latest` | Unprivileged nginx serving the SPA and proxying API/SEO routes; host port `8080` |

The shipped mapping is `8080:8080`, so `web` listens on **all host interfaces**. It serves plain HTTP. Place
it behind a TLS-terminating reverse proxy and use a firewall or network policy to prevent untrusted direct
access to port `8080`.

The stack uses two networks: `frontend`, shared by `web` and `api`, and internal-only `backend`, shared by
`api` and `db`. Persistent state is split across these volumes:

| Volume               | Contents                                                           |
| -------------------- | ------------------------------------------------------------------ |
| `db-data`            | PostgreSQL data                                                    |
| `api-files`          | Uploaded files                                                     |
| `api-dataprotection` | ASP.NET Data Protection keys and the generated Ed25519 certificate |
| `api-state`          | The immutable demo/normal deployment-mode choice                   |

Logs go to stdout and are available through `docker compose logs`. Automatic email waiting in memory is not
persistent.

Both application containers run as non-root, drop Linux capabilities, use `no-new-privileges` and have
read-only root filesystems with explicit writable mounts. Health checks target `/api/auth/csrf` for the API
and `/healthz` for nginx.

### Capacity and rate limits

nginx is configured with 8,192 connections per worker, a 4,000-active-client-connection server boundary and
a 2,000-active-connection allowance per IP. Its per-IP request limits are flood controls sized to tolerate
mobile carrier NAT and VPN concentration; application quotas are enforced by ASP.NET Core per authenticated
user wherever an identity is available. The `web` container raises its open-file limit to 65,536 so the
configured worker capacity is effective.

The shipped budgets are a baseline for one 4-core, 8-GiB host running the Compose stack. They target 1,000
concurrently active users with ordinary, staggered SPA interaction, not 1,000 simultaneous password hashes
or long-running requests. Credential concurrency stays at four because each Argon2id operation allocates
64 MiB; excess work queues only 16 requests before returning `429`. Across the API, 128 requests may execute
and 256 wait. Reports allow 16 concurrent executions, while file writes allow 12 so ten administrators can
upload simultaneously without saturating the process. Synchronous administrator email is limited to two
active bulk dispatches because one request may contact up to 500 recipients; single-recipient messages allow
ten active dispatches. Validate the target on production-equivalent storage, PostgreSQL and SMTP
infrastructure with a workload that includes login, cached public reads, authenticated reads, writes, uploads
and reports. Rate limits protect capacity but do not establish it.

## First production start

```bash
curl -LO https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/docker-compose.yml
curl -Lo .env https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/.env.example
# Edit .env and replace all required or placeholder values.
docker compose up -d
docker compose ps
docker compose logs api
```

Set at least:

- `POSTGRES_PASSWORD`: a new value of at least 16 characters.
- `DATA_PROTECTION_CERTIFICATE_PASSWORD`: a separate value of at least 32 characters.
- `APP_BASE_URL`: the final public HTTPS origin, with no path, query or fragment.
- `DEMO_MODE`: the permanent mode for these volumes.
- `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD`: used only when the user table is empty.
- `SMTP_HOST`, `SMTP_PORT`, `SMTP_SECURITY`, `SMTP_FROM_ADDRESS` and any required SMTP credentials.

Every login is completed with a one-time code, emailed unless the user enrolled an authenticator
application, so startup fails in every environment when `SMTP_HOST` or `SMTP_FROM_ADDRESS` is missing.
Production validation additionally requires an encrypted SMTP mode (`StartTls` or `SslOnConnect`) and valid
host, port and sender values. `APP_BASE_URL` must use a public DNS name; IP addresses, localhost and reserved
example/test domains are rejected.

### TLS and proxy boundary

The external proxy must terminate HTTPS, overwrite client-supplied forwarding headers, and send the effective
scheme as `X-Forwarded-Proto`. The API accepts one forwarded hop, redirects HTTP to HTTPS in Production, and
sets secure `__Host-` cookies. nginx emits HSTS only when the forwarded scheme is HTTPS.

`APP_BASE_URL` controls links in email and the URLs generated in `/sitemap.xml` and `/robots.txt`; it must
match the public origin.

## Environment variables

The base Compose file forwards an explicit variable list to the API. Adding an arbitrary .NET
`SECTION__KEY` value to `.env` has no effect until the same variable is added under `api.environment` in
`docker-compose.yml`.

| Variable                               | Meaning                                                                            | Template value                                 |
| -------------------------------------- | ---------------------------------------------------------------------------------- | ---------------------------------------------- |
| `POSTGRES_HOST`                        | Database host used by the API                                                      | `db`                                           |
| `POSTGRES_PORT`                        | Database port                                                                      | `5432`                                         |
| `POSTGRES_DB`                          | Database name                                                                      | `codigoactivo`                                 |
| `POSTGRES_USER`                        | Database user                                                                      | `codigoactivo`                                 |
| `POSTGRES_PASSWORD`                    | Database password; 16+ characters in Production                                    | Empty                                          |
| `DATA_PROTECTION_CERTIFICATE_PASSWORD` | Encrypts the generated Ed25519 private key; 32+ characters                         | Empty                                          |
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

The template also declares `PGDATA`, but the current Compose file does not forward it. PostgreSQL therefore
uses the image's default data directory inside the volume mounted at `/var/lib/postgresql`.

For a direct `dotnet run`, missing database variables fall back to `localhost:5432` and database/user
`codigoactivo`; the password remains empty. Missing `APP_BASE_URL` falls back to
`http://localhost:5173`. SMTP has no fallback: direct development points `SMTP_HOST` at a mail catcher such
as the Mailpit service of the development overlay (`localhost:1025`, security `None`), which is also where
the login codes can be read.

### Application settings

Defaults in `backend/src/CodigoActivo.API/appsettings.json` include:

| Setting                                     | Default             |
| ------------------------------------------- | ------------------- |
| `Auth:ExpireHours`                          | `8`                 |
| `FileStorage:MaxSizeBytes`                  | `10485760` (10 MiB) |
| `AccountVerification:OtpLifetimeMinutes`    | `15`                |
| `AccountVerification:ResendCooldownSeconds` | `60`                |
| `PasswordReset:CodeLifetimeMinutes`         | `15`                |
| `PasswordReset:ResendCooldownSeconds`       | `60`                |
| `TwoFactor:ChallengeLifetimeMinutes`        | `10`                |
| `TwoFactor:ResendCooldownSeconds`           | `60`                |
| `TwoFactor:AuthenticatorSetupLifetimeMinutes` | `15`              |
| `TwoFactor:MaxFailedAttempts`               | `5`                 |
| `TwoFactor:LockoutMinutes`                  | `15`                |
| `TwoFactor:Issuer`                          | `Código Activo`     |
| `ManualEmail:MaxRecipients`                 | `500`               |
| `ManualEmail:MaxAttachments`                | `10`                |
| `ManualEmail:MaxAttachmentsBytes`           | `8388608` (8 MiB)   |

Standard .NET environment syntax can override these values, for example `AUTH__EXPIREHOURS`. In Docker, add
the uppercase variable to the Compose API environment list as well.

The upload limit is enforced by the API and accompanied by 64 KiB of multipart overhead. nginx separately
limits request bodies to `12m`; increasing the application limit may also require changing
`frontend/docker/default.conf`. Manual-email attachments share the API request limit and are never stored in
`api-files`.

## Email delivery

Automatic messages pass through a process-local rate limiter and a bounded in-memory queue. Administrator
bulk email is synchronous, uses a single SMTP connection, and bypasses the automatic-message budget so its
response can report delivered, failed and skipped recipients.

### Automatic email guard

The guard cannot be disabled. Missing, invalid or non-positive settings fall back to defaults.

| Setting                              | Purpose                                             | Default |
| ------------------------------------ | --------------------------------------------------- | ------- |
| `EmailGuard:RecipientBurst`          | Immediate messages allowed per normalized address   | `20`    |
| `EmailGuard:RecipientPerHour`        | Sustained hourly limit per address                  | `10`    |
| `EmailGuard:RecipientPerDay`         | Daily limit per address                             | `50`    |
| `EmailGuard:GlobalBurst`             | Immediate process-wide budget                       | `1000`  |
| `EmailGuard:GlobalPerHour`           | Sustained process-wide hourly budget                | `1000`  |
| `EmailGuard:GlobalCredentialReserve` | Global capacity reserved for verification and reset | `200`   |
| `EmailGuard:MaxTrackedRecipients`    | Maximum address budgets held in memory              | `50000` |
| `EmailGuard:SweepIntervalMinutes`    | Idle-budget cleanup interval                        | `5`     |
| `EmailGuard:AlertIntervalMinutes`    | Minimum interval between repeated guard alerts      | `15`    |

Quota is consumed when the send is accepted, before it enters the queue. Limits are per API process: a
restart refills them, and additional replicas multiply them.

### Automatic email queue

| Setting                           | Purpose                                    | Default |
| --------------------------------- | ------------------------------------------ | ------- |
| `EmailQueue:Capacity`             | Maximum queued messages                    | `1000`  |
| `EmailQueue:Workers`              | Concurrent SMTP workers, capped at 16      | `4`     |
| `EmailQueue:ShutdownDrainSeconds` | Graceful drain time, capped at 300 seconds | `20`    |
| `EmailQueue:SendTimeoutSeconds`   | Per-message timeout, capped at 600 seconds | `60`    |

The queue has no persistence and no retry. Delivery failures are logged without rolling back the already
committed application action. A full queue rejects new automatic messages after their guard quota is spent.
The shutdown drain must remain below the API service's 30-second `stop_grace_period`.

Monitor logs for recipient throttling warnings, global-budget exhaustion, a full queue, SMTP delivery errors
and undelivered messages at shutdown.

## Demo mode and initial administrator

On first start, the API writes the selected `DEMO_MODE` value to `/app/state/deployment-mode` in `api-state`.
Later starts must use the same value. Demo mode seeds realistic content and invented accounts under
`demo.codigoactivo.es` whose passwords are random and discarded, so they cannot be used to log in; the
person giving the demonstration signs in with the bootstrap administrator from `.env`, which is the only
administrator and receives its login codes at `BOOTSTRAP_ADMIN_EMAIL` through the configured SMTP server.

Changing mode requires destroying all named volumes and therefore all application data:

```bash
docker compose down -v
docker compose up -d
```

> [!CAUTION]
> `docker compose down -v` permanently deletes the database, uploads, Data Protection keys and mode lock. Use
> it only for a deliberately disposable environment.

When the user table is empty, startup requires a valid bootstrap email and a 12–128 character password and
creates the first active administrator before accepting requests. Once any user exists, both bootstrap
variables are ignored; changing them does not reset, replace or promote an account.

## Development overlay

Inside the repository, `docker compose up --build` merges `docker-compose.override.yml` and:

- builds `api` and `web` from the working tree;
- sets the API environment to Development and exposes port `5150`;
- adds a Mailpit service, points the API's SMTP settings at it and exposes its inbox on port `8025`, so
  verification links and login codes can be read without a real mail server;
- exposes PostgreSQL on port `5432` and makes the backend network non-internal;
- disables the API read-only root filesystem and adds debugger-oriented privileges.

Visual Studio uses the same Compose project through `backend/docker-compose.dcproj`.

## Releases and upgrades

CI runs on pushes and pull requests targeting `master`. A successful CI run on `master` triggers Docker
Publish for the API and UI independently.

| Image    | Version source                           | Git release tag |
| -------- | ---------------------------------------- | --------------- |
| Backend  | `<Version>` in `CodigoActivo.API.csproj` | `vX.X.X-API`    |
| Frontend | `version` in `frontend/package.json`     | `vX.X.X-UI`     |

The publisher accepts strict `X.X.X` versions and skips an image when its release tag already exists. A new
version pushes both the immutable version tag and `latest`, with provenance and an SBOM, before creating the
Git tag. The production Compose file follows `latest`.

Upgrade with:

```bash
docker compose pull
docker compose up -d
```

Review logs and run application smoke tests after the containers become healthy. PostgreSQL 18 is mounted at
`/var/lib/postgresql`; the repository does not provide an in-place upgrade procedure from older major
versions.

## Backups and recovery

Back up `db-data`, `api-files`, `api-dataprotection` and `api-state`. Test restoration regularly. Keep
`DATA_PROTECTION_CERTIFICATE_PASSWORD` separately from the `api-dataprotection` backup; losing either makes
the protected key ring unusable and invalidates sessions.

The email queue is intentionally absent from backups. A restart or forced shutdown can lose pending mail, but
the database action that requested it has already committed.

Before public exposure, verify TLS and forwarded headers, firewall access to port `8080`, SMTP delivery,
database and volume recovery, registration and email verification, the two-step login, password reset,
authorization changes, file access and the selected demo mode. Review [SECURITY.md](SECURITY.md) for the
complete security model.
