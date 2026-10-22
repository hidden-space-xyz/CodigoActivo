# Deployment

The production distribution is the root `docker-compose.yml`. It pulls released images from GitHub Container
Registry and reads configuration from a sibling `.env` file and secrets from a sibling `secrets/` directory;
it does not require a repository clone.

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

The shipped mapping is `0.0.0.0:8080:8080`, so `web` listens on **all host IPv4 interfaces** over plain HTTP
and expects a TLS-terminating proxy in front. Networks: `frontend` (shared by `web` and `api`) and
internal-only `backend` (shared by `api` and `db`), both with IPv6 disabled. The application accepts IPv4
clients only: see [TLS and proxy boundary](#tls-and-proxy-boundary).

| Volume               | Contents                                                           |
| -------------------- | ------------------------------------------------------------------ |
| `db-data`            | PostgreSQL data, including the API's diagnostic and audit logs     |
| `api-files`          | Uploaded images, re-encoded as two WebP variants each              |
| `api-dataprotection` | ASP.NET Data Protection keys and the generated Ed25519 certificate |
| `api-state`          | The immutable demo/normal deployment-mode choice                   |
| `logs-db`            | PostgreSQL log files, one per weekday                              |

The API stores its log in two PostgreSQL tables, which administrators read on the «Diagnóstico» and
«Auditoría» pages and the API purges itself; PostgreSQL writes its own log files into `logs-db` and overwrites
each one a week later. See [SECURITY.md](SECURITY.md#logging) for what is logged and how retention works.
`db` and `web` disable
container logging (`logging: driver: none`); `api` keeps a capped `json-file` log (2 files of 1 MB) for what
cannot reach the database: a failed start and failures of the logging pipeline itself. Queued email is
persisted in `db-data`, not held in memory; see [Email delivery](#email-delivery).

Both application containers run as non-root, drop Linux capabilities, use `no-new-privileges` and have
read-only root filesystems with explicit writable mounts. `db` also uses `no-new-privileges` and keeps only
the capabilities its entrypoint needs to fix the volume ownership and switch to the `postgres` user
(`CHOWN`, `DAC_OVERRIDE`, `FOWNER`, `SETGID`, `SETUID`); its entrypoint also hands `logs-db` to that user.
Health checks target `/api/auth/csrf` (API) and `/healthz` (nginx).

nginx gzips text responses of 1 KiB or more on the fly, both static files and proxied API responses; the API
itself does not compress. The `web` image build also stores a `.gz` sibling of each text asset, which nginx
serves directly (`gzip_static`). A reverse proxy in front must not strip `Accept-Encoding`.

### Capacity and rate limits

nginx allows 8,192 connections per worker, 4,000 active client connections and 2,000 per IP (`web`'s open-file
limit is raised to 65,536). Application request and credential-work rate limits are documented once, in
[SECURITY.md](SECURITY.md#passwords-and-credential-endpoints).

## First production start

```bash
curl -LO https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/docker-compose.yml
curl -Lo .env https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/.env.example
# Edit .env and replace all required or placeholder values.
mkdir -m 700 secrets
openssl rand -base64 32 | tr -d '\r\n' > secrets/postgres_password
openssl rand -base64 48 | tr -d '\r\n' > secrets/data_protection_certificate_password
touch secrets/smtp_password
docker compose up -d
docker compose ps
docker compose logs api
docker compose exec db sh -c 'tail -n 100 /var/log/postgresql/postgresql-*.log'
```

Set at least `APP_BASE_URL` (final public HTTPS origin, no path/query/fragment), `DEMO_MODE` (permanent for
these volumes), `BOOTSTRAP_ADMIN_EMAIL` (used only when the user table is empty), `SMTP_HOST` and
`SMTP_FROM_ADDRESS`, the association's identity for the privacy policy (`LEGAL_NAME`, `LEGAL_TAX_ID`,
`LEGAL_ADDRESS`, `LEGAL_REGISTRY`, `LEGAL_EMAIL`), plus `SMTP_USERNAME` and its password in
`secrets/smtp_password` when the server needs a login. See [Secrets](#secrets) for the file rules.

`docker compose logs api` shows only a failed start (its fatal event) and failures of the logging pipeline;
everything else the API logs is on the «Diagnóstico» page.

Every login is completed with a one-time code, emailed unless the user enrolled an authenticator app, so
startup fails everywhere when `SMTP_HOST` or `SMTP_FROM_ADDRESS` is missing or `SMTP_PORT` is outside 1-65535.
Production startup (`ProductionConfiguration`) also fails when:

- `POSTGRES_PASSWORD` has fewer than 16 characters or `DATA_PROTECTION_CERTIFICATE_PASSWORD` fewer than 32;
- `APP_BASE_URL` is not a clean HTTPS origin on a public DNS name (IP addresses, localhost and reserved
  example/test domains are rejected);
- the SMTP host or sender is invalid, `SMTP_SECURITY` is `None` or `Auto`, or only one of `SMTP_USERNAME` and
  `secrets/smtp_password` is set;
- any of `LEGAL_NAME`, `LEGAL_TAX_ID`, `LEGAL_ADDRESS`, `LEGAL_REGISTRY` or `LEGAL_EMAIL` is blank.

These settings and every application setting are checked before anything is written to `api-state` or the
database. `BOOTSTRAP_ADMIN_EMAIL` is checked once the database is migrated and the deployment mode is locked
only after that check, so a start it stops leaves a migrated database but no mode lock. Demo seeding runs after
the lock; a start it stops keeps the lock and seeds again on the next start. On a first Production start,
the Data Protection certificate is created in `api-dataprotection` after the checks above but before the
application settings are validated.

### TLS and proxy boundary

The external proxy must terminate HTTPS, overwrite client-supplied forwarding headers and send the effective
scheme as `X-Forwarded-Proto`. nginx normalizes that header itself before forwarding it to the API: only an
exact `https` value (case-insensitive) counts, so any other value — including a comma-separated list such as
`https, http` — or a missing header becomes `http`. Because nginx only reads the header value, not the peer,
the external proxy must overwrite `X-Forwarded-Proto` rather than append to it; appending makes nginx see
`http`, so it skips HSTS and forwards `http` to the API. Neither nginx nor the API redirects HTTP to HTTPS (the
container has no HTTPS port), so the external proxy must redirect or refuse plain HTTP itself. nginx emits HSTS
only when its normalized scheme is HTTPS. The API accepts one forwarded hop and honours
`X-Forwarded-For`/`X-Forwarded-Proto` only from IPv4 loopback and private peers (`127.0.0.0/8` and RFC 1918),
so a proxy or container network outside those ranges makes it ignore both headers and put every client in a
single shared rate-limit partition. The proxy must also pass the public host name in `Host`: in Production the
API only accepts the host of `APP_BASE_URL` (plus `localhost`, `127.0.0.1` and `api`) and answers any other
with 400. IPv6 is not supported: nginx answers 403 to every request whose client address, read from
`X-Forwarded-For`, is IPv6. `APP_BASE_URL` controls links in email and the URLs generated in `/sitemap.xml`
and `/robots.txt`; it must match the public origin.

## Environment variables

These variables are the whole deployment configuration: the base Compose file forwards exactly this list to
the API, and every other value is fixed in code (see [Fixed settings](#fixed-settings)). Compose fixes the
database connection itself (host `db`, port `5432`, database and user `codigoactivo`), so the API only
reaches PostgreSQL over the internal `backend` network. Optional variables take the listed default when empty
or missing.

| Variable                        | Meaning                                                                       | Default                                     | Template value                                 |
| ------------------------------- | ----------------------------------------------------------------------------- | ------------------------------------------- | ---------------------------------------------- |
| `APP_BASE_URL`                  | Public origin for links and SEO output                                        | Required                                    | `https://example.org` (invalid for Production) |
| `APP_TIMEZONE`                  | IANA time-zone ID used by the application clock                               | `Europe/Madrid`                             | `Europe/Madrid`                                |
| `DEMO_MODE`                     | `true` or `false`; locked on first start                                      | Required                                    | `false`                                        |
| `BOOTSTRAP_ADMIN_EMAIL`         | Initial administrator email for an empty database                             | Required on an empty database               | Empty                                          |
| `SMTP_HOST`                     | SMTP host; always required because login codes are emailed                    | Required                                    | Empty                                          |
| `SMTP_PORT`                     | SMTP port                                                                     | `587`                                       | `587`                                          |
| `SMTP_SECURITY`                 | `StartTls`, `SslOnConnect`, `None` or `Auto`; Production rejects the last two | `SslOnConnect` on port 465, else `StartTls` | Empty                                          |
| `SMTP_USERNAME`                 | SMTP username; set exactly when `secrets/smtp_password` is not empty          | No login                                    | Empty                                          |
| `SMTP_FROM_ADDRESS`             | Single sender address                                                         | Required                                    | Empty                                          |
| `SMTP_FROM_NAME`                | Sender display name                                                           | `Código Activo`                             | Empty                                          |
| `LEGAL_NAME`                    | Registered name of the association, shown as data controller                  | Required in Production                      | Empty                                          |
| `LEGAL_TAX_ID`                  | NIF of the association                                                        | Required in Production                      | Empty                                          |
| `LEGAL_ADDRESS`                 | Registered postal address of the association                                  | Required in Production                      | Empty                                          |
| `LEGAL_REGISTRY`                | Register of associations and entry number                                     | Required in Production                      | Empty                                          |
| `LEGAL_EMAIL`                   | Contact mailbox on the site and in the footer of news email                   | Required in Production                      | Empty                                          |
| `LEGAL_PHONE`                   | Contact phone published in the footer, the about page and the privacy policy  | Not published                               | Empty                                          |

The site and news email read the association's legal identity and contact details from the `LEGAL_*`
variables (the site through `GET /api/legal-entity`); a `LEGAL_EMAIL` that is not a single address or a
`LEGAL_PHONE` that is not a phone number of 7 to 15 digits stops the start. They are deployment settings
rather than code so that personal details, such as a home address or a private phone, never reach the public
repository; the association's public identity (name, domain, social profiles) is code, in `Organization`
(backend) and `shared/config` (frontend). The privacy policy texts state
the log retentions and the hosting, email and file storage providers; change those texts in the same release
as any of them.

Neither the template nor Compose defines `PGDATA`; PostgreSQL uses the default data directory inside the
volume mounted at `/var/lib/postgresql`. The container clock runs in UTC, as do the PostgreSQL log files
and the daily partitions of the API's log tables; only the application clock follows `APP_TIMEZONE`, which
the log pages also use for their day filters.

### Secrets

Secrets are Compose file secrets, never environment variables, so they stay out of `docker inspect`,
`docker compose config` and the process environment. Each file in the ignored `secrets/` directory is
mounted at `/run/secrets/<name>`, and the API reads it as the configuration key named after the file
(`postgres_password` is `POSTGRES_PASSWORD`); a secret file wins over an environment variable with the same
name.

| File                                           | Meaning                                                                                          |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `secrets/postgres_password`                    | Database password, read by PostgreSQL on its first initialization and by the API; 16+ characters |
| `secrets/data_protection_certificate_password` | Encrypts the Ed25519 private key and the key ring; 32+ characters, not rotatable                 |
| `secrets/smtp_password`                        | SMTP password; empty when the server needs no login                                              |

All three files must exist. A single trailing newline is ignored, but a carriage return is not: PostgreSQL
strips it from its password and the API keeps it, so a CRLF file (as Windows tools write) fails the database
login. Keep the directory private to the deployment account (`0700`) and the files readable (`0644`): Compose
without Swarm bind-mounts them unchanged, and the containers read them as unprivileged users.

For a direct `dotnet run`, secrets are plain environment variables with those uppercase names. Missing
database variables fall back to `localhost:5432` and database/user `codigoactivo` with an empty password;
missing `APP_BASE_URL` falls back to `http://localhost:5173` and missing `APP_TIMEZONE` to the machine's time
zone. SMTP has no fallback: point `SMTP_HOST` at a
mail catcher such as the development overlay's Mailpit (`localhost:1025`, security `None`), where login codes
can also be read.

### Fixed settings

Security, abuse and retention values are fixed in code, so no deployment can weaken them through
configuration: lifetimes, lockouts and resend waits of passwords, sessions and one-time codes, upload and
email limits, the email guard and queue, and the retention of logs and accounts. `appsettings.json` holds only
the framework's log levels and host filter. Changing a fixed value is a code change, released like any other.

| Value                                                            | Fixed at                                    |
| ---------------------------------------------------------------- | ------------------------------------------- |
| Session lifetime                                                 | 30 days                                     |
| Second-factor challenge, the emailed login code included         | 10 minutes                                  |
| Verification code, password reset link and authenticator setup   | 15 minutes                                  |
| Wait before another emailed code to the same account             | 60 seconds                                  |
| Wrong passwords before the account locks until a password reset  | 5                                           |
| Wrong second-factor codes before verification locks              | 5, for 15 minutes                           |
| Uploaded image                                                   | 10 MiB                                      |
| Administrator email                                              | 500 recipients, 10 attachments, 8 MiB total |
| Diagnostic and audit log entries                                 | 30 and 60 days                              |

Kestrel refuses request bodies above 2 MiB except on the upload and manual-email endpoints, which accept the
upload limit plus 64 KiB of multipart overhead; nginx separately limits request bodies to `12m`. Manual-email
attachments are never stored in `api-files`.

The cookie and privacy policies and the login checkbox (`pages.login.keepSignedIn`) state the session and
challenge lifetimes and the retentions; changing one requires updating them in
`frontend/src/shared/i18n/locales/es.json`.

## Email delivery

Every email — automatic messages and administrator-written bulk mail alike — is stored in a PostgreSQL
outbox (`email_outbox_messages`, `email_outbox_contents`, `email_outbox_content_parts`) and delivered in the
background by the `EmailOutboxDeliveryWorker` hosted service; a request that queues email returns once the row
is committed, not once SMTP accepts it. Automatic messages pass through a process-local rate limiter
before reaching the outbox; administrator bulk email bypasses that budget but is queued the same way, so its
response (`{ queued, skipped }`) reports what was accepted, not what was delivered.

The guard can be neither disabled nor configured (`EmailGuardOptions`). Limits are per API process:
restarts refill them, replicas multiply them.

| Limit                                                                    | Value  |
| ------------------------------------------------------------------------ | ------ |
| Immediate messages allowed per normalized address                        | 20     |
| Sustained hourly limit per address                                       | 10     |
| Daily limit per address                                                  | 50     |
| Immediate process-wide budget                                            | 1000   |
| Sustained process-wide hourly budget                                     | 1000   |
| Global capacity reserved for verification, reset and second-factor codes | 200    |
| Maximum address budgets held in memory                                   | 50000  |
| Idle-budget cleanup interval                                             | 5 min  |
| Minimum interval between repeated guard alerts or outbox failures        | 15 min |

Verification and reset mail cannot spend the last half of the reserved capacity, which stays for
second-factor codes, and second-factor codes have per-address budgets of their own.

Pending mail survives a restart: rows are claimed with `FOR UPDATE SKIP LOCKED` and a lease, in priority
order (2FA/verification/reset codes first, ordinary automatic mail next, administrator-written mail last), by
a poll loop every 5 seconds, also woken immediately by same-process enqueues; each pass claims up to 10 due
messages and sends 4 at a time, each within 60 seconds. A message is attempted up to 5 times, retried after
1 minute, 5 minutes, 30 minutes and 2 hours on the first four failures; the 5th failure discards the row and
logs an error. Delivery is at-least-once: a crash between a successful send and removing the row can resend
it. The outbox accepts about 1000 pending ordinary automatic and bulk messages; login, verification and
password-reset codes are always accepted (and still bounded by the `ThrottledEmailSender` budgets above).
Shutdown only waits for sends already in flight, up to 20 seconds, below the API service's 30-second
`stop_grace_period`; whatever is not delivered by then stays in the outbox for the next start. These values
live in `EmailQueueOptions` and cannot be configured.

A full outbox (`EmailOutboxFull`), an exhausted global budget (`GlobalEmailBudgetExhausted`) and exhausted
attempts (`EmailDeliveryGaveUp`, with the last error) are logged as `Error`, and each delivery failure as the
`Warning` event `EmailDeliveryAttemptFailed`. That event, a failed outbox run and a
failed removal of spent messages repeat while their cause lasts, so each is logged at most once every 15
minutes. Recipient throttling, a low global budget and an incomplete shutdown drain
are not logged. See [SECURITY.md](SECURITY.md#logging) for where these events are written.

News email carries the one-click unsubscribe of RFC 8058 (`List-Unsubscribe` pointing at
`APP_BASE_URL/api/emails/unsubscribe/one-click?token=…`, and `List-Unsubscribe-Post`). The API does not sign
mail: mail clients only offer the button when the relay behind `SMTP_HOST` signs it with DKIM for the domain
of `SMTP_FROM_ADDRESS` and the signature covers both headers. The proxy in front of nginx must pass that `POST`
through to `APP_BASE_URL` as it is, without a redirect (RFC 8058 forbids them), a login wall or a bot
challenge, since the mail provider's servers send it without cookies.

## Disposable email domains

The API downloads the disposable email domain list over HTTPS from `raw.githubusercontent.com` 30 seconds
after startup and then daily, so `api` needs outbound HTTPS through the `frontend` network. Without it the
site keeps working: the last valid list stays in `disposable_email_domains` (in `db-data`), or every domain
is accepted if none was ever stored. Restarting `api` forces a new download. Validation and matching rules
are in [SECURITY.md](SECURITY.md#email-abuse-controls).

The source is fixed in code: the project's `disposable_email_blocklist.conf` on its `main` branch. Failures log
`DisposableEmailDomainDownloadFailed` and `DisposableEmailDomainListRejected` (`Warning`, retried hourly) and
`DisposableEmailDomainListNotStored` (`Error`); while they repeat, new disposable domains are not blocked.

## Locked accounts

Five wrong passwords lock an account for good: the lock has no expiry and is lifted only by a completed
password reset, which needs working SMTP. Anyone who knows an account's email can lock accounts in bulk; each
lock logs `PasswordLockoutTriggered`. If every administrator is locked while email is
unavailable, unlock against the `db` container:

```bash
docker compose exec db psql -U codigoactivo -d codigoactivo -c \
  "UPDATE users SET password_failed_attempts = 0, password_locked_at = NULL WHERE email = 'admin@example.org';"
```

## Demo mode and initial administrator

On first start the API writes the selected `DEMO_MODE` value to `/app/state/deployment-mode` in `api-state`;
later starts must use the same value. The file is always written except when the API runs in Development
outside a container (the API image declares `DOTNET_RUNNING_IN_CONTAINER=true`, and `1` is accepted too), so
a local `dotnet run` still validates `DEMO_MODE` and writes nothing to the developer's filesystem, while the
Compose development overlay still locks the mode in `api-state`.
Demo mode seeds realistic content and invented accounts under `demo.codigoactivo.es` with random, discarded
passwords, so they cannot log in, and gives the bootstrap administrator invented dependents, recorded like any
minor as declared by their guardian, here that administrator. The demonstrator
signs in with the bootstrap administrator, the only administrator, whose password reset and login codes arrive
at `BOOTSTRAP_ADMIN_EMAIL` through the configured SMTP server. Seeding downloads its sample images from
`picsum.photos` (three attempts each, at most five minutes in total), so `api` needs outbound HTTPS there on
the start that seeds; a failed download stops that start.
Demo mode also fills the diagnostic and audit logs with an invented three-week history that follows the demo
data: logins, signups, published content, a password attack, email outages and slow requests. A later start
writes a new one once the audit log holds no entry of a demo account younger than the diagnostic retention;
entries already past the retention of their log are left out. A failure to write it is logged as a warning
and does not stop the start.

Changing mode requires destroying all named volumes and therefore all application data:

```bash
docker compose down -v
docker compose up -d
```

> [!CAUTION]
> `docker compose down -v` permanently deletes the database, uploads, Data Protection keys and mode lock. Use
> it only for a deliberately disposable environment.

When the user table is empty, startup requires a valid bootstrap email and creates the first active
administrator before accepting requests, with a random password that is discarded: set the first password
through the login page's password reset for that email, which also proves the mailbox and SMTP work. It is
seeded with the fixed DNI/NIE `00000000T`; change it from the administrator's profile after first login, since
no other account can register with that value while it is in use. Once any user exists, the bootstrap email
is ignored. This generic account has a fixed id, can be neither deleted nor demoted, and receives the content
of deleted accounts; startup refuses a database that has users but not that account.

## Development overlay

Inside the repository, `docker compose up --build` merges `docker-compose.override.yml`: it builds `api` and
`web` from the working tree, sets the API to Development and exposes port `5150`, adds a Mailpit service
publishing SMTP on port `1025` and its inbox UI on port `8025` and points every `SMTP_*` value at it (the
`.env` SMTP settings are ignored), exposes PostgreSQL on port `5432` and makes
the backend network non-internal, and disables the API read-only root filesystem in favor of
debugger-oriented privileges. Visual Studio uses the same Compose project through
`backend/docker-compose.dcproj`. In Development the API also writes its log to the console, which the overlay
keeps at 5 MB per file, two files.

## Releases and upgrades

Only pushes to `master` start CI, merged pull requests included; open pull requests and pushes to `develop`
run none of it. Backend build/unit/integration tests and frontend checks must pass before publishing. The Docker workflow
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
review `docker compose logs api`, the «Diagnóstico» page and `logs-db` (see [SECURITY.md](SECURITY.md#logging))
and smoke test.
