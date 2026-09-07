# Deployment

The app is deployed as a **Docker Compose** stack running the released images from GitHub Container
Registry. The root `docker-compose.yml` references no local files and carries no configuration of its
own, so that single file — copied anywhere — is a complete deployment; every variable resolves from a
git-ignored `.env` file next to it, created from the `.env.example` template. For local development
without containers, see
[CONTRIBUTING.md](CONTRIBUTING.md#local-setup); for the security rationale behind the hardening below, see
[SECURITY.md](SECURITY.md).

## The stack

`docker-compose.yml` defines three services:

| Service | Image / build                                        | Role                                                                                         |
| ------- | ---------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| **db**  | `postgres:18.4-alpine3.24`                           | PostgreSQL 18. On the **internal** `backend` network only (not published in production).      |
| **api** | `ghcr.io/hidden-space-xyz/codigoactivo-backend:latest` | ASP.NET Core API, listens on `:8080`, `ASPNETCORE_ENVIRONMENT=Production`, hardened container. |
| **web** | `ghcr.io/hidden-space-xyz/codigoactivo-frontend:latest` (nginx unprivileged) | Serves the SPA and reverse-proxies `/api` (plus the root `/sitemap.xml` and `/robots.txt`) → `api:8080`. Published on `127.0.0.1:8080`. |

The two app images are the ones CI publishes on release ([Published images](#published-images)); the
development override builds them from the working tree instead (see
[Local / debug overlay](#local--debug-overlay)).

**Networks**: `frontend` (bridge) and `backend` (internal — the DB is unreachable from outside).
**Volumes**: `db-data` (database), `api-files` (uploads), `api-dataprotection` (ASP.NET Data
Protection keys), and `api-state` (the immutable deployment-mode selection). Logs go to stdout only —
read them with `docker compose logs`, no volume involved.
Both `api` and `web` run as non-root with
capabilities dropped; the `api` container additionally runs with a read-only filesystem and a
`HEALTHCHECK` against `/api/auth/csrf` (the `web` container checks `/healthz`).

In the `web` image the built SPA and the nginx config stay **root-owned and world-readable** rather
than being chowned to the runtime user: the unprivileged nginx user (uid 101) has to be able to read
them and must never be able to modify them. `frontend/Dockerfile` carries no comments, so that is
recorded here.

## Published images

Every push to `master` first runs the `CI` workflow. Only a successful CI completion triggers `Docker Publish`
(`.github/workflows/docker-publish.yml`), which scans, builds and pushes the two images to GitHub Container
Registry as two independent pipelines — each versioned inside its own project:

| Image                                            | Version source                                                        | Release tag  |
| ------------------------------------------------ | --------------------------------------------------------------------- | ------------ |
| `ghcr.io/hidden-space-xyz/codigoactivo-backend`  | `<Version>` in `backend/src/CodigoActivo.API/CodigoActivo.API.csproj` | `vX.X.X-API` |
| `ghcr.io/hidden-space-xyz/codigoactivo-frontend` | `version` in `frontend/package.json`                                  | `vX.X.X-UI`  |

For each image the workflow reads its version (strictly `X.X.X` — anything else fails the run) and
**skips the build entirely if that version's release tag already exists** on the repository. Bumping
the version in a project is what releases that image; merging to `master` without bumping publishes
nothing. When it does build, it pushes the `<version>` and `latest` image tags with provenance and an SBOM,
and only then creates the git release tag — so a run that fails before tagging is simply retried by the next
merge. Compose intentionally follows `latest` for both first-party application images.

## Production

Copy `docker-compose.yml` to the server (no clone needed), create its `.env` from the `.env.example`
template, and start the stack:

```bash
curl -LO https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/docker-compose.yml
curl -Lo .env https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/.env.example
nano .env
docker compose up -d
```

The template intentionally contains placeholders and the API refuses an unsafe Production startup. Adjust at
least these before starting ([Environment variables](#environment-variables)):

- `POSTGRES_PASSWORD` — use at least 16 random characters (e.g. `openssl rand -base64 32`);
- `DATA_PROTECTION_CERTIFICATE_PASSWORD` — a separate random secret of at least 32 characters; it
  encrypts the Ed25519 private key used to sign the encrypted session and antiforgery key ring;
- `APP_BASE_URL` — the public `https://` URL, used in links, outgoing emails and the sitemap;
- `DEMO_MODE` — choose `true` or `false` permanently for this set of volumes;
- `ACCOUNT_VERIFICATION_REQUIRED` — choose whether new accounts must verify their email;
- `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD` — credentials for the administrator that
  startup creates as the first user in a new database;
- the `SMTP_*` block, including `StartTls` or `SslOnConnect`, for email delivery;

The PostgreSQL 18 image uses `/var/lib/postgresql/18/docker` inside a volume mounted at
`/var/lib/postgresql`. This project intentionally includes no PostgreSQL 17 compatibility or in-place upgrade
path; discard any pre-release local `db-data` volume with `docker compose down -v` before the first start.

To upgrade the two first-party application images to the latest successful release:

```bash
docker compose pull && docker compose up -d
```

> [!WARNING]
> **Deploying from a clone instead?** Then always pass `-f docker-compose.yml`: inside the repo a
> bare `docker compose up` also merges `docker-compose.override.yml` (the development overlay
> described below), which builds from source, relaxes the container hardening and exposes the
> database — you do not want that on a server. A standalone copy of `docker-compose.yml` has no
> override to worry about.

### TLS / reverse proxy

> [!IMPORTANT]
> The `web` container terminates **plain HTTP** on fixed `127.0.0.1:8080`. Put it behind an external
> TLS-terminating reverse proxy that sets `X-Forwarded-Proto`, and set `APP_BASE_URL` to the public
> `https://` URL.

The API has forwarded headers enabled and, in Production, issues `Secure` cookies and redirects
HTTP → HTTPS. `APP_BASE_URL` is used in links, outgoing emails and every URL of the generated
`/sitemap.xml` and `/robots.txt` — if it is left at the template's `https://example.org` placeholder,
search engines receive a sitemap full of unusable URLs and Production refuses to start. Session and CSRF
cookies use the fixed `SameSite=Lax` policy required by the same-origin deployment.

## Local / debug overlay

Inside the repo, `docker compose up --build` (without `-f`) auto-merges `docker-compose.override.yml`,
which turns the deployment stack into the development one:

- `api` and `web` are **built from the working tree** (`backend/src/CodigoActivo.API/Dockerfile`,
  `frontend/Dockerfile`) and tagged `codigoactivo-api-dev` / `codigoactivo-web-dev` instead of pulling
  the GHCR images.
- `api` switches to `ASPNETCORE_ENVIRONMENT=Development` and is published on `5150:8080` (Swagger at
  `/swagger`), with hardening relaxed (`read_only: false`, `SYS_PTRACE`) so a debugger can attach.
- `db` is published on `127.0.0.1:5432` and the `backend` network is made non-internal.

**Visual Studio** picks up the same override on **F5** via `backend/docker-compose.dcproj` — set
`docker-compose` as the startup project to build the API in `Debug` and step through the container.

## Environment variables

Administrator-controlled runtime configuration is supplied as flat environment variables. Each variable
below is passed 1:1 from `.env`, so that file always starts as a copy of `.env.example` (whose shipped values
are the last column). Listener settings, persistent application paths, cookie policy and credential-work
limits are fixed in code or Compose. The PostgreSQL data path remains administrator-configurable, and the
connection string is built from `POSTGRES_*` in code.

| Variable                        | Description                                                        | `.env.example` ships            |
| ------------------------------- | ----------------------------------------------------------------- | ------------------------------- |
| `POSTGRES_HOST`                 | Database host (code falls back to `localhost` for a bare `dotnet run`) | `db`                       |
| `POSTGRES_PORT`                 | Database port                                                     | `5432`                          |
| `POSTGRES_DB`                   | Database name                                                     | `codigoactivo`                  |
| `POSTGRES_USER`                 | Database user                                                     | `codigoactivo`                  |
| `POSTGRES_PASSWORD`             | Database password — **required**, Postgres refuses to initialize with it empty (e.g. `openssl rand -base64 32`) | *(empty)* |
| `DATA_PROTECTION_CERTIFICATE_PASSWORD` | Separate 32+ character secret used to encrypt the private key of the locally generated Ed25519 certificate | *(empty)* |
| `PGDATA`                        | PostgreSQL 18 data directory inside the `db-data` volume           | `/var/lib/postgresql/18/docker` |
| `APP_BASE_URL`                  | Public base URL used in links, outgoing emails and the generated sitemap/robots (code falls back to `http://localhost:5173`) | `https://example.org` |
| `APP_TIMEZONE`                  | IANA/Windows time zone for the app clock                          | `Europe/Madrid`                 |
| `DEMO_MODE`                     | Initial deployment mode (`true` for demo, `false` for normal); valid in Development and Production and immutable after first start | `false` |
| `ACCOUNT_VERIFICATION_REQUIRED` | Optionally require email (OTP) verification before login in any environment | `true`                 |
| `BOOTSTRAP_ADMIN_EMAIL`         | Email of the administrator created as the first user of an empty database | *(empty)* |
| `BOOTSTRAP_ADMIN_PASSWORD`      | 12–128 character password for that initial administrator          | *(empty)* |
| `SMTP_HOST`                     | SMTP server — **required if verification is enabled**, and to send any email at all | *(empty)*     |
| `SMTP_PORT`                     | SMTP port                                                         | `587`                           |
| `SMTP_SECURITY`                 | `StartTls` / `SslOnConnect` / `None` / `Auto`                     | `StartTls`                      |
| `SMTP_USERNAME` · `SMTP_PASSWORD` | SMTP credentials                                                | *(empty)*                       |
| `SMTP_FROM_ADDRESS`             | Sender address — **required if verification is enabled**, and to send any email at all | *(empty)* |
| `SMTP_FROM_NAME`                | Sender display name                                               | *(empty)*                       |

A handful of app-internal knobs live in `backend/src/CodigoActivo.API/appsettings.json` (log levels,
`Auth:CookieName` = `CodigoActivo.Session`, `Auth:ExpireHours` = `8`, `FileStorage:MaxSizeBytes` = 10 MiB,
`AccountVerification:OtpLifetimeMinutes` = `15`, `ResendCooldownSeconds` = `60`, `ManualEmail:MaxRecipients`
= `500`, `ManualEmail:MaxAttachments` = `10`, `ManualEmail:MaxAttachmentsBytes` = 8 MiB, plus the
`EmailGuard` and `EmailQueue` sections below). Override any of them, if needed, with the standard .NET
`Section__Key` environment-variable convention. Keep every variable name in `.env` uppercase (for
example, `AUTH__EXPIREHOURS`).

> [!IMPORTANT]
> `SECTION__KEY` overrides only reach the API if the variable is actually passed into the container. The
> `api` service in `docker-compose.yml` declares an **explicit list** of environment variables and has no
> `env_file:`, so putting `AUTH__EXPIREHOURS` or `EMAILGUARD__RECIPIENTBURST` in the root `.env` has no
> effect on the Docker stack until you add the name to that list. Only the flat variables in the table
> above are wired through.

The `EmailGuard` section tunes the outbound email guard, which rate-limits every **automatic** email
(verification, password reset, activity notifications) and never the manual email admins send. **The guard
is always on** — there is no switch that disables it, and any key left out, zero, negative or unparseable
falls back to the default below rather than lifting the limit:

| Key                               | Meaning                                                        | Default |
| --------------------------------- | -------------------------------------------------------------- | ------- |
| `EmailGuard:RecipientBurst`       | Messages one address may receive back-to-back                  | `20`    |
| `EmailGuard:RecipientPerHour`     | Sustained hourly rate per address                              | `10`    |
| `EmailGuard:RecipientPerDay`      | Sustained daily ceiling per address                            | `50`    |
| `EmailGuard:GlobalBurst`          | Automatic messages the process may send back-to-back           | `1000`  |
| `EmailGuard:GlobalPerHour`        | Sustained hourly rate over all automatic mail                  | `1000`  |
| `EmailGuard:GlobalCredentialReserve` | Slice of the global budget only verification/reset may use  | `200`   |
| `EmailGuard:MaxTrackedRecipients` | Address budgets held in memory before falling back to global   | `50000` |
| `EmailGuard:SweepIntervalMinutes` | How often idle address budgets are evicted                     | `5`     |
| `EmailGuard:AlertIntervalMinutes` | Minimum gap between repeated guard alerts in the log           | `15`    |

The `EmailQueue` section tunes the in-process queue that delivers every automatic email in the background, so
no user request ever waits on the SMTP relay. It holds messages in memory only — nothing is persisted, and
nothing is retried. Same posture as `EmailGuard`: a missing, zero, negative or unparseable value falls back
to its default — and, unlike `EmailGuard`, an out-of-range value is **clamped** rather than accepted
(`Workers` ≤ 16, `ShutdownDrainSeconds` ≤ 300, `SendTimeoutSeconds` ≤ 600).

| Key                              | Meaning                                                           | Default |
| -------------------------------- | ----------------------------------------------------------------- | ------- |
| `EmailQueue:Capacity`            | Messages held before new ones are refused as a guard denial       | `1000`  |
| `EmailQueue:Workers`             | Concurrent SMTP connections the queue may use to drain            | `4`     |
| `EmailQueue:ShutdownDrainSeconds`| How long shutdown waits for the queue to empty                    | `20`    |
| `EmailQueue:SendTimeoutSeconds`  | Ceiling on one delivery, so a hung relay cannot wedge a worker    | `60`    |

> [!NOTE]
> `Workers` trades relay pressure against queue latency. Verification and password-reset codes expire 15
> minutes after the **request**, not after delivery, so a backlog that drains more slowly than that mails out
> codes that are already dead. Lower it only if your relay rejects concurrent connections; raise it only if
> you have measured a backlog.

> [!IMPORTANT]
> `EmailQueue:ShutdownDrainSeconds` must stay **below** the `api` service's `stop_grace_period` (30 s in
> `docker-compose.yml`), or Docker kills the container mid-drain and the pending messages are lost silently.

> [!NOTE]
> `FileStorage:MaxSizeBytes` drives both the HTTP request-size limit on the upload endpoints (+64 KiB of
> multipart overhead) and the business-rule check. In the Docker stack, nginx additionally caps request
> bodies at `client_max_body_size 12m` (`frontend/docker/default.conf`) — raising the knob past ~12 MiB
> requires raising that too.

> [!NOTE]
> The admin "send email" endpoints are multipart too, and reuse that same transport limit for the whole
> request (subject + body + **all** attachments), so keep `ManualEmail:MaxAttachmentsBytes` below it. The
> attachments are never written to `/app/files`. A bulk send is synchronous — one message per
> recipient over a single SMTP connection — and nginx allows it up to `proxy_read_timeout 300s`
> (`frontend/docker/proxy-api.conf`); `ManualEmail:MaxRecipients` is what keeps a single send inside it.

> [!NOTE]
> **What to watch in the logs.** The API logs one `Information` line at startup naming every guard cap. A
> burst of `Warning`s naming the same
> `{Recipient}` means the guard is holding a mailbomb (the address is logged once per throttling episode, not
> once per dropped message). A single `Error` saying the global budget is exhausted means automatic mail has
> stopped until it refills; read the `{Kind}` values preceding it to see which flow drained it. Admin-written
> email keeps working throughout. Budgets are in-memory: a restart refills them, and more than one `api`
> replica multiplies every cap by the replica count.
>
> Because automatic mail is now delivered in the background, a broken `SMTP_*` configuration no longer shows
> up as a failing request — the registration or signup decision succeeds and the failure surfaces later as repeated
> `Error`s from the dispatcher naming the `{Kind}` and `{Recipient}` it could not deliver. Two more lines are
> worth alerting on: an `Error` saying the queue "is full" (the relay has been down long enough to back up
> 1000 messages) and a `Warning` at shutdown saying messages "were left undelivered".

## Demo mode

On the first start, the API writes the selected `DEMO_MODE` value to the fixed
`/app/state/deployment-mode` path in the `api-state` volume. Every later start must present the same value;
changing `.env` alone makes startup fail.
This lock is independent of `ASPNETCORE_ENVIRONMENT`, so demo and normal mode can each run in Development or
Production.

With `DEMO_MODE=true`, `DemoDataSeeder` adds a full, realistic dataset after the initial administrator. It
downloads placeholder images from picsum.photos and creates demo accounts, including an additional demo admin
with the password `Demo1234!`. To select a different mode, recreate the complete stack and all named volumes:

```bash
docker compose down -v
docker compose up -d
```

The first command irreversibly deletes the database, uploads, Data Protection keys and the mode file. On the
next start, the current `DEMO_MODE` value becomes the new permanent selection.

> [!CAUTION]
> Production permits demo mode, but the seeded accounts use the public password `Demo1234!`. Treat such a
> deployment as public, disposable demonstration data and never store private information in it.

## First administrator

An empty database requires `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD`. During startup, after the
catalogs are available and before any demo seed runs or HTTP traffic is accepted, the API inserts an active
administrator with those credentials. Its initial profile name is “Administrador Código Activo”, its gender
is “Other”, its role is “Socio”, and its placeholder birth date is 2000-01-01; it can edit those profile
fields after login.

If at least one user already exists, both bootstrap variables are ignored completely on every later restart.
Changing them cannot create, replace, promote or reset an account. Public registration always creates a
non-administrator, so the bootstrap account is necessarily the first user inserted into a new database.

## Production release gate

Before exposing the TLS virtual host, all of the following must be true:

- CI is green, including backend integration tests against PostgreSQL, frontend build/lint/format, npm audit
  and CodeQL; review the dependency audit results.
- `APP_BASE_URL` is the final clean HTTPS origin, the persisted `DEMO_MODE` selection and optional account
  verification setting are intentional, and SMTP is encrypted and tested.
- `POSTGRES_PASSWORD` and SMTP credentials are newly generated production secrets; `.env` is readable only by
  the deployment account and is not copied into images or backups without encryption.
- `DATA_PROTECTION_CERTIFICATE_PASSWORD` is newly generated, stored separately from volume backups and
  included in the secret-recovery procedure; losing it invalidates every protected session key.
- The web port remains loopback-only unless a firewall provides an equivalent trust boundary; only the TLS
  proxy is internet-facing and it overwrites `X-Forwarded-For`/`X-Forwarded-Proto`.
- The first-party API/UI images use the `latest` release published after a successful CI run; the database,
  uploads and Data Protection key volumes have tested backup and restore procedures.
- Register, login, password reset, upload/download authorization, admin demotion and account blocking are
  smoke-tested through the public HTTPS URL; include account verification when it is enabled.

## Data & backups

> [!IMPORTANT]
> The named volumes hold all state. Back up `db-data` (database), `api-files` (uploads), `api-state` (the
> immutable mode selection) and `api-dataprotection` regularly. The Data Protection key ring is wrapped with
> AES-256-GCM and every wrapper is signed by a self-signed Ed25519 certificate generated in the latter volume;
> the Ed25519 private key is encrypted with `DATA_PROTECTION_CERTIFICATE_PASSWORD`. Back up that volume and
> password separately. The
> one exception is outbound email still sitting in the in-memory queue: it is deliberately not persisted, so
> a restart that outlasts the drain window drops it. Nothing user-visible depends on it — the write that
> triggered the mail is already committed, and members can always request a new verification code.
