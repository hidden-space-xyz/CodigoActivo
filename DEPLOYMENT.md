# Deployment

The app is deployed as a **Docker Compose** stack. All configuration and secrets come from a single,
git-ignored root `.env` file (copy `.env.example`). For local development without containers, see
[CONTRIBUTING.md](CONTRIBUTING.md#local-setup); for the security rationale behind the hardening below, see
[SECURITY.md](SECURITY.md).

## The stack

`docker-compose.yml` defines three services:

| Service | Image / build                                        | Role                                                                                         |
| ------- | ---------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| **db**  | `postgres:17-alpine`                                 | PostgreSQL. On the **internal** `backend` network only (not published in production).         |
| **api** | built from `backend/src/CodigoActivo.API/Dockerfile` | ASP.NET Core API, listens on `:8080`, `ASPNETCORE_ENVIRONMENT=Production`, hardened container. |
| **web** | built from `frontend/Dockerfile` (nginx unprivileged) | Serves the SPA and reverse-proxies `/api` (plus the root `/sitemap.xml` and `/robots.txt`) → `api:8080`. Published on `${HTTP_PORT:-8080}:8080`. |

**Networks**: `frontend` (bridge) and `backend` (internal — the DB is unreachable from outside).
**Volumes**: `db-data` (database), `api-files` (uploads), `api-logs` (Serilog output),
`api-dataprotection` (ASP.NET Data Protection keys). Both `api` and `web` run as non-root with
capabilities dropped; the `api` container additionally runs with a read-only filesystem and a
`HEALTHCHECK` against `/api/auth/csrf` (the `web` container checks `/healthz`).

## Production

```bash
cp .env.example .env                              # set POSTGRES_PASSWORD, APP_BASE_URL, timezone, SMTP, …
docker compose -f docker-compose.yml up -d --build
```

> [!WARNING]
> **Always pass `-f docker-compose.yml`.** A bare `docker compose up` also merges
> `docker-compose.override.yml` (the development overlay described below), which relaxes the container
> hardening and exposes the database — you do not want that on a server.

### TLS / reverse proxy

> [!IMPORTANT]
> The `web` container terminates **plain HTTP** on `${HTTP_PORT}`. Put it behind an external
> TLS-terminating reverse proxy that sets `X-Forwarded-Proto`, and set `APP_BASE_URL` to the public
> `https://` URL.

The API has forwarded headers enabled and, in Production, issues `Secure` cookies and redirects
HTTP → HTTPS. `APP_BASE_URL` is used in links, outgoing emails and every URL of the generated
`/sitemap.xml` and `/robots.txt` — if it is left at the compose fallback (`https://localhost`),
search engines receive a sitemap full of unusable URLs. Set `AUTH_SAMESITE` to match your
cross-site needs.

## Local / debug overlay

`docker compose up` (without `-f`) auto-merges `docker-compose.override.yml`:

- `api` switches to `ASPNETCORE_ENVIRONMENT=Development` and is published on `5150:8080` (Swagger at
  `/swagger`), with hardening relaxed (`read_only: false`, `SYS_PTRACE`) so a debugger can attach.
- `db` is published on `127.0.0.1:5432` and the `backend` network is made non-internal.

**Visual Studio** picks up the same override on **F5** via `backend/docker-compose.dcproj` — set
`docker-compose` as the startup project to build the API in `Debug` and step through the container.

## Environment variables

Runtime configuration is supplied as flat environment variables (template: `.env.example`). Compose injects
them into the `api` service; the connection string is built from `POSTGRES_*` in code.

| Variable                        | Description                                                        | Default                         |
| ------------------------------- | ----------------------------------------------------------------- | ------------------------------- |
| `POSTGRES_HOST`                 | Database host                                                      | `localhost` (`db` in Compose)   |
| `POSTGRES_PORT`                 | Database port                                                     | `5432`                          |
| `POSTGRES_DB`                   | Database name                                                     | `codigoactivo`                  |
| `POSTGRES_USER`                 | Database user                                                     | `codigoactivo`                  |
| `POSTGRES_PASSWORD`             | Database password — **required**                                  | *(none)*                        |
| `APP_BASE_URL`                  | Public base URL used in links, outgoing emails and the generated sitemap/robots | `http://localhost:5173`         |
| `APP_TIMEZONE`                  | IANA/Windows time zone for the app clock                          | host local (image sets `Europe/Madrid`) |
| `AUTH_SAMESITE`                 | Session/CSRF cookie `SameSite` — `Lax` / `Strict` / `None`        | `Lax`                           |
| `DEMO_MODE`                     | Seed/purge realistic demo data on startup (see below)             | `false`                         |
| `ACCOUNT_VERIFICATION_REQUIRED` | Require email (OTP) verification before login                     | `true` in code; `.env.example` ships `false` |
| `SMTP_HOST`                     | SMTP server — **required if verification is enabled**             | *(none)*                        |
| `SMTP_PORT`                     | SMTP port                                                         | `587`                           |
| `SMTP_SECURITY`                 | `StartTls` / `SslOnConnect` / `None` / `Auto`                     | `StartTls`                      |
| `SMTP_USERNAME` · `SMTP_PASSWORD` | SMTP credentials                                                | *(none)*                        |
| `SMTP_FROM_ADDRESS`             | Sender address — **required if verification is enabled**          | *(none)*                        |
| `SMTP_FROM_NAME`                | Sender display name                                               | `Código Activo`                 |
| `FILE_STORAGE_ROOT`             | Directory for uploaded files                                     | `files` (`/app/files` in container) |
| `HTTP_PORT`                     | Host port the `web` container publishes                          | `8080`                          |

A handful of app-internal knobs live in `backend/src/CodigoActivo.API/appsettings.json` (Serilog levels,
`Auth:CookieName` = `CodigoActivo.Session`, `Auth:ExpireHours` = `8`, `FileStorage:MaxSizeBytes` = 10 MiB,
`AccountVerification:OtpLifetimeMinutes` = `15`, `ResendCooldownSeconds` = `60`). Override any of them, if
needed, with the standard .NET `Section__Key` environment-variable convention (e.g. `Auth__ExpireHours`).

> [!NOTE]
> `FileStorage:MaxSizeBytes` drives both the HTTP request-size limit on the upload endpoints (+64 KiB of
> multipart overhead) and the business-rule check. In the Docker stack, nginx additionally caps request
> bodies at `client_max_body_size 12m` (`frontend/docker/default.conf`) — raising the knob past ~12 MiB
> requires raising that too.

## Demo mode

Setting `DEMO_MODE=true` seeds a full, realistic demo dataset on startup via `DemoDataSeeder` (it downloads
placeholder images from picsum.photos and creates demo accounts, including an admin with the password
`Demo1234!`). Flipping it back to `false` **removes** the demo data on the next startup. It is off by
default and backend-only.

> [!CAUTION]
> Never enable `DEMO_MODE` in a real deployment — the demo admin uses a well-known password (`Demo1234!`).

## Data & backups

> [!IMPORTANT]
> The named volumes hold all state. Back up `db-data` (database) and `api-files` (uploads) regularly, and
> keep `api-dataprotection` stable across restarts so existing session/antiforgery cookies stay valid.
