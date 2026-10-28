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
| `api-dataprotection` | ASP.NET Data Protection keys, encrypted with `DATA_PROTECTION_KEY` |
| `api-state`          | The immutable demo/normal deployment-mode choice                   |
| `logs-db`            | PostgreSQL log files, one per weekday                              |

Neither the template nor Compose defines `PGDATA`; PostgreSQL uses the default data directory inside the
volume mounted at `/var/lib/postgresql`. The container clock runs in UTC, as do the PostgreSQL log files
and the daily partitions of the API's log tables; only the application clock follows `APP_TIMEZONE`, which
the log pages also use for their day filters.

The API stores its log in two PostgreSQL tables, which administrators read on the «Diagnóstico» and
«Auditoría» pages, and PostgreSQL writes its own log files into `logs-db`; container logging keeps only what
cannot reach the database. [SECURITY.md](SECURITY.md#logging) describes what each log holds and every
retention. Queued email is persisted in `db-data`, not held in memory; see [Email delivery](#email-delivery).

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

### Backups

Back up the `api-dataprotection` volume with every `db-data` backup and keep `secrets/data_protection_key`:
without both the data history cannot be read. Data Protection adds a key every 90 days, so a copy of the volume
older than the database copy cannot read the entries protected with the keys created since.

## First production start

```bash
curl -LO https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/docker-compose.yml
curl -Lo .env https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/.env.example
# Edit .env and set the required values.
mkdir -m 700 secrets
openssl rand -base64 32 | tr -d '\r\n' > secrets/postgres_password
openssl rand -base64 32 | tr -d '\r\n' > secrets/data_protection_key
touch secrets/smtp_password
docker compose up -d
docker compose ps
docker compose logs api
docker compose exec db sh -c 'tail -n 100 /var/log/postgresql/postgresql-*.log'
```

Set at least `APP_BASE_URL` (final public HTTPS origin, no path/query/fragment), `DEMO_MODE` (permanent for
these volumes), `BOOTSTRAP_ADMIN_EMAIL` (used only when the user table is empty), `PRIVACY_OFFICER_EMAIL`,
`SMTP_HOST` and `SMTP_FROM_ADDRESS`, the association's identity for the privacy policy (`LEGAL_NAME`,
`LEGAL_TAX_ID`, `LEGAL_ADDRESS`, `LEGAL_REGISTRY`, `LEGAL_EMAIL`), plus `SMTP_USERNAME` and its password in
`secrets/smtp_password` when the server needs a login. See [Secrets](#secrets) for the file rules.

`docker compose logs api` shows only a failed start (its fatal event) and failures of the logging pipeline;
everything else the API logs is on the «Diagnóstico» page.

Every login is completed with a one-time code, emailed unless the user enrolled an authenticator app, so
startup fails everywhere when `SMTP_HOST` or `SMTP_FROM_ADDRESS` is missing or `SMTP_PORT` is outside 1-65535.
It also fails everywhere when `DATA_PROTECTION_KEY` is not 32 random bytes in Base64 (bytes that are all
printable, as in an encoded password, or all equal are refused) or does not decrypt a key already in the key
ring. Production startup (`ProductionConfiguration`) also fails when:

- `POSTGRES_PASSWORD` has fewer than 16 characters;
- `APP_BASE_URL` is not a clean HTTPS origin on a public DNS name (IP addresses, localhost and reserved
  example/test domains are rejected);
- the SMTP host or sender is invalid, `SMTP_SECURITY` is `None` or `Auto`, or only one of `SMTP_USERNAME` and
  `secrets/smtp_password` is set;
- any of `LEGAL_NAME`, `LEGAL_TAX_ID`, `LEGAL_ADDRESS`, `LEGAL_REGISTRY` or `LEGAL_EMAIL` is blank.

These settings and every application setting, `DEMO_MODE` included, are checked before anything is written to
`api-state` or the database, and so is a `DEMO_MODE` other than the locked one. `BOOTSTRAP_ADMIN_EMAIL` is
checked once the database is migrated and the mode is locked only after that check, so a start it stops has
migrated the database but written no mode lock. Demo seeding runs after the lock; a start it stops keeps the
lock and seeds again on the next start.

The privacy and cookie policies are not in the repository: administrators write and publish them in
«Políticas» of the administration panel, and the database keeps every version. A new installation without demo
data starts without them, and `/privacy` and `/cookies` say they are not published yet, so publish both before
opening the site. They state the log retentions, the cookies and the hosting, email and file storage
providers; publish new versions in the same release as any change to them.

### TLS and proxy boundary

The external proxy must terminate HTTPS and redirect or refuse plain HTTP itself: neither nginx nor the API
redirects (the container has no HTTPS port). It must overwrite client-supplied forwarding headers and send the
effective scheme as `X-Forwarded-Proto`, replacing any value rather than appending to it: nginx counts only an
exact `https` (case-insensitive), so an appended list such as `https, http` or a missing header reads as `http`,
and nginx then skips HSTS and forwards `http` to the API. The proxy and the Compose networks must use IPv4
loopback or private addresses (`127.0.0.0/8` and RFC 1918): the forwarded client address is only trusted from
those, so any other puts every client in a single shared rate-limit partition, and IPv6 clients get 403.
[SECURITY.md](SECURITY.md#transport-and-proxy-trust) explains which header each hop trusts and why. The proxy
must also pass the public host name in `Host`: in Production the API only accepts the host of `APP_BASE_URL`
(plus `localhost`, `127.0.0.1` and `api`) and answers any other with 400. `APP_BASE_URL` controls links in
email and the URLs generated in `/sitemap.xml` and `/robots.txt`; it must match the public origin.

## Environment variables

These variables are the whole deployment configuration: the base Compose file forwards exactly this list to
the API, and every other value is fixed in code (see [Fixed settings](#fixed-settings)). Compose fixes the
database connection itself (host `db`, port `5432`, database and user `codigoactivo`), so the API only
reaches PostgreSQL over the internal `backend` network. Optional variables take the listed default when empty
or missing. `.env.example` lists every variable with an empty value.

| Variable                | Meaning                                                                       | Default                                     |
| ----------------------- | ----------------------------------------------------------------------------- | ------------------------------------------- |
| `APP_BASE_URL`          | Public origin for links and SEO output                                        | Required                                    |
| `APP_TIMEZONE`          | IANA time-zone ID used by the application clock                               | `Europe/Madrid`                             |
| `DEMO_MODE`             | `true` or `false`; locked on first start                                      | Required                                    |
| `BOOTSTRAP_ADMIN_EMAIL` | Initial administrator email for an empty database                             | Required on an empty database               |
| `PRIVACY_OFFICER_EMAIL` | Email of the administrator designated for data protection                     | Required                                    |
| `SMTP_HOST`             | SMTP host; always required because login codes are emailed                    | Required                                    |
| `SMTP_PORT`             | SMTP port                                                                     | `587`                                       |
| `SMTP_SECURITY`         | `StartTls`, `SslOnConnect`, `None` or `Auto`; Production rejects the last two | `SslOnConnect` on port 465, else `StartTls` |
| `SMTP_USERNAME`         | SMTP username; set exactly when `secrets/smtp_password` is not empty          | No login                                    |
| `SMTP_FROM_ADDRESS`     | Single sender address                                                         | Required                                    |
| `SMTP_FROM_NAME`        | Sender display name                                                           | `Código Activo`                             |
| `SMTP_MAX_PER_MINUTE`   | Messages the SMTP provider accepts per minute; delivery never goes faster     | `30`                                        |
| `SMTP_MAX_PER_DAY`      | Messages the SMTP provider accepts in a rolling 24 hours                      | `2000`                                      |
| `LEGAL_NAME`            | Registered name of the association, shown as data controller                  | Required in Production                      |
| `LEGAL_TAX_ID`          | NIF of the association                                                        | Required in Production                      |
| `LEGAL_ADDRESS`         | Registered postal address of the association                                  | Required in Production                      |
| `LEGAL_REGISTRY`        | Register of associations and entry number                                     | Required in Production                      |
| `LEGAL_EMAIL`           | Contact mailbox on the site and in the footer of news email                   | Required in Production                      |
| `LEGAL_PHONE`           | Contact phone published in the footer, the about page and the privacy policy  | Not published                               |

The site and news email read the association's legal identity and contact details from the `LEGAL_*`
variables (the site through `GET /api/legal-entity`); a `LEGAL_EMAIL` that is not a single address or a
`LEGAL_PHONE` that is not a phone number of 7 to 15 digits stops the start. They are deployment settings
rather than code so that personal details, such as a home address or a private phone, never reach the public
repository; the association's public identity (name, domain, social profiles) is code, in `Organization`
(backend) and `shared/config` (frontend).

`PRIVACY_OFFICER_EMAIL` designates the only account that may consult the blocked data (see
[SECURITY.md](SECURITY.md#blocked-data-consultations)): the administrator whose verified email it is, whatever
their second factor. Use the personal mailbox of the person the board designated in writing, never a shared one
such as `LEGAL_EMAIL`, since whoever reads the mailbox can reset the password and, while the account uses
emailed codes, also receives its second factor. A value that is not a single address stops the start; to change
the person, change it and restart the API. The development overlay defaults it to `BOOTSTRAP_ADMIN_EMAIL`.

### Secrets

Secrets are Compose file secrets, never environment variables, so they stay out of `docker inspect`,
`docker compose config` and the process environment. Each file in the ignored `secrets/` directory is
mounted at `/run/secrets/<name>`, and the API reads it as the configuration key named after the file
(`postgres_password` is `POSTGRES_PASSWORD`); a secret file wins over an environment variable with the same
name.

| File                          | Meaning                                                                                           |
| ----------------------------- | ------------------------------------------------------------------------------------------------- |
| `secrets/postgres_password`   | Database password, read by PostgreSQL on its first initialization and by the API; 16+ characters  |
| `secrets/data_protection_key` | 32 random bytes in Base64; encrypts the key ring and derives the one-time code key; not rotatable |
| `secrets/smtp_password`       | SMTP password; empty when the server needs no login                                               |

All three files must exist. A single trailing newline is ignored, but a carriage return is not: PostgreSQL
strips it from its password and the API keeps it, so a CRLF file (as Windows tools write) fails the database
login. Keep the directory private to the deployment account (`0700`) and the files readable (`0644`): Compose
without Swarm bind-mounts them unchanged, and the containers read them as unprivileged users. PostgreSQL reads
its password only when it initializes an empty volume, so rotating it means changing both the `codigoactivo`
role and `secrets/postgres_password`.

For a direct `dotnet run`, secrets are plain environment variables with those uppercase names. Missing
`POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB` and `POSTGRES_USER` fall back to `localhost:5432` and
database/user `codigoactivo`, with an empty password; missing `APP_BASE_URL` falls back to
`http://localhost:5173` and missing `APP_TIMEZONE` to the machine's time zone. SMTP has no fallback: point
`SMTP_HOST` at a mail catcher such as the development overlay's Mailpit (`localhost:1025`, security `None`),
where login codes can also be read.

### Fixed settings

Security, abuse and retention values are fixed in code, so no deployment can weaken them through
configuration: lifetimes, lockouts and resend waits of passwords, sessions and one-time codes, rate and
concurrency limits, request, upload and email limits, the email guard and queue, and the retention of logs,
accounts and the data history. Their values are documented with the control they belong to, in
[SECURITY.md](SECURITY.md) and, for the email queue, in [Email delivery](#email-delivery). `appsettings.json`
holds only the framework's log levels and host filter. Changing a fixed value is a code change, released like
any other.

The login checkbox (`pages.login.keepSignedIn` in `frontend/src/shared/i18n/locales/es.json`) and the
published cookie and privacy policies state the session and challenge lifetimes and the retentions; changing
one requires updating the checkbox and publishing new versions of the policies.

## Email delivery

Every email — automatic messages and administrator-written bulk mail alike — is stored in a PostgreSQL
outbox (`email_outbox_messages`, `email_outbox_contents`, `email_outbox_content_parts`) and delivered in the
background by the `EmailOutboxDeliveryWorker` hosted service; a request that queues email returns once the row
is committed, not once SMTP accepts it. Only codes pass the email guard first (limits in
[SECURITY.md](SECURITY.md#email-abuse-controls)); notices are always stored, and a new message on the same
topic to the same address (a new code, a new alert of one kind, a new decision on one signup) replaces the one
still waiting. Administrator bulk email is refused with `EmailSendFailed` when 2000 such messages already wait,
or with `EmailAttachmentsTooLarge` when it is larger than the `SIZE` the SMTP server announced; its response
(`{ queued, skipped }`) reports what was accepted, not what was delivered.

One API instance delivers at a time: the worker holds a PostgreSQL session lock and another instance takes over
when it is released. It polls every 5 seconds, is woken at once by same-process enqueues, and claims up to 10
due messages with `FOR UPDATE SKIP LOCKED` and a lease, in priority order: login and other identified codes,
then verification, email-in-use and password-reset mail, then notices, then administrator-written mail. A run
sends them one after another over one SMTP connection, each within 60 seconds, never faster than
`SMTP_MAX_PER_MINUTE`. Over a rolling 24 hours, counted in `email_delivery_hours` so restarts keep it, notices
and bulk mail stop at 80 % of `SMTP_MAX_PER_DAY`, anonymous codes at 90 % and identified codes at 100 %.
Notices and bulk mail are also paced per recipient (20 at once, 10 per hour, 50 per day): a message over its
recipient's share waits, without spending an attempt.

A recipient whose address or mailbox is refused for good (5xx with enhanced status 5.1 or 5.2), a message too
large or content the key ring cannot read is removed and logged. A failure that may pass, such as a recipient
the server defers (4xx) or refuses for another reason (a relay's policy or quota) or any other reply to the
message, is retried after 1, 5 and 30 minutes and then every hour. An unreachable server, a refused login or
sender, a `421`, or three different messages in a row failing for an unknown cause (copies of one mailing count
once) pause the whole delivery for 1, 2, 5, 10 and then 30 minutes, handing the claimed messages back untried. Messages expire instead
of running out of attempts: codes with their own lifetime, other mail after 3 days; the coming-of-age notice
and the notices to the legal contact never expire. Delivery is at-least-once: a crash between a successful send
and removing the row can resend it. Shutdown only waits for the send in flight, up to 20 seconds, below the API
service's 30-second `stop_grace_period`, and hands the rest of the claimed messages back untried, due at once; a
send still running by then is claimed again once its lease expires. These values live in `EmailQueueOptions` and cannot be configured; only the provider pace is.

The legal contact (`LEGAL_EMAIL`) is told about withdrawn consents and opened consultations of the blocked data
by `LegalNoticeWorker`, 30 seconds after startup and then every minute: each kind goes in one email listing up
to 50 of them, and the next one waits until that email has left the outbox. What it tells stays marked in the
database until the email is queued, so nothing is lost to a failure or a restart.

A pause (`EmailDeliveryPaused`), expired messages (`EmailsExpired`), notices waiting more than 24 hours
(`EmailsOverdue`) and an exhausted anonymous budget (`GlobalEmailBudgetExhausted`) are logged as `Error`; a
refused message (`EmailRejected`), a nearly spent daily limit (`EmailDailyLimitReached`) and each failed attempt
(`EmailDeliveryAttemptFailed`) as `Warning`. Repeated pauses, attempt failures, daily-limit alerts, failed runs
and failed removals are logged at most once every 15 minutes. Recipient pacing and an incomplete shutdown drain
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

An account locked by wrong passwords stays locked until a completed password reset
([SECURITY.md](SECURITY.md#passwords-and-credential-endpoints)), which needs working SMTP; each lock logs
`PasswordLockoutTriggered`. If every administrator is locked while email is unavailable, unlock against the
`db` container:

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
Demo mode seeds realistic content and invented accounts under `demo.codigoactivo.es`, which
[cannot sign in](SECURITY.md#demo-mode), and gives the bootstrap administrator invented dependents, recorded
like any minor as declared by their guardian, here that administrator. The demonstrator signs in with the
bootstrap administrator, the only administrator, whose password reset and login codes arrive at
`BOOTSTRAP_ADMIN_EMAIL` through the configured SMTP server. Seeding downloads its sample images from
`picsum.photos` (three attempts each, at most five minutes in total), so `api` needs outbound HTTPS there on
the start that seeds; a failed download stops that start.
Demo mode also fills the diagnostic and audit logs with an invented three-week history that follows the demo
data: logins, signups, published content, a password attack, email outages and slow requests. A later start
writes a new one once the audit log holds no entry of a demo account younger than the diagnostic retention;
entries already past the retention of their log are left out. A failure to write it is logged as a warning
and does not stop the start.
It also replays a year of changes and erasures of personal data through the data history, so «Archivo
legal» has something to consult: demo accounts that replaced their email and phone, withdrew their news
consent, left an activity or deleted a rating, and three former accounts erased by their holder together with
a minor (Lozano Prieto), by the administrator (Ibáñez Robles) and by the purge of unverified accounts (Gallego
Herrera). It runs once, in one transaction, while the history holds nothing about those former accounts; a
failure stops that start.

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
publishing SMTP on port `1025` and its inbox UI on port `8025` and points every `SMTP_*` value at it, fixes
`APP_BASE_URL`, `APP_TIMEZONE`, `DEMO_MODE=true` and every `LEGAL_*` value (so `.env` only needs
`BOOTSTRAP_ADMIN_EMAIL`), exposes PostgreSQL on port `5432` and makes the backend network non-internal, and
disables the API read-only root filesystem in favor of debugger-oriented privileges. Visual Studio uses the same
Compose project through `backend/docker-compose.dcproj`. In Development the API also writes its log to the
console, which the overlay keeps at 5 MB per file, two files.

## Releases and upgrades

Only pushes to `master` start CI, merged pull requests included; open pull requests and pushes to `develop`
run none of it. The backend build, its architecture, unit and integration tests with 90% coverage, and the
frontend checks must pass before publishing. The Docker workflow is a reusable CI stage; it does not run
independently or on a schedule.

Pushes to `develop` or `master` that change `backend/`, `frontend/`, `tests/` or the workflow itself start the
CodeQL workflow, which is independent of CI and does not gate publishing. A newer push to the same branch
cancels the scan in progress.
Findings never fail the run; review them in the repository's code scanning alerts or in the
`codeql-sarif-<language>` workflow artifacts, retained for 7 days. Only a broken build, analysis or upload fails it.
C# analysis uses a full .NET 10 build under CodeQL tracing, including source generators and test projects;
results located in `obj/` or `bin/` (generated code) are dropped before upload. JavaScript/TypeScript uses
the build-free analysis mode.

API and UI share one version and one GitHub release, tagged `vX.Y.Z`. All repository commits since the
highest stable `vX.Y.Z` tag count. With no matching tag, the baseline is `0.0.0` and the full commit history
is considered.
The largest Conventional Commit increment wins: `feat` = minor, `fix`/`perf` = patch, any type with `!` = major.
Any other type (`refactor`, `docs`, `chore`, Dependabot's `bump`, ...) and non-conventional subjects produce no
increment.
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
