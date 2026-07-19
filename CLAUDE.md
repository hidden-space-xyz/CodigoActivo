# CLAUDE.md

Guidance for Claude Code when working in this repository. **Each app has its own `CLAUDE.md` — read the one for the folder you're editing.**

- **[backend/CLAUDE.md](backend/CLAUDE.md)** — ASP.NET Core Web API (.NET 10, EF Core + PostgreSQL, 5-project clean architecture).
- **[frontend/CLAUDE.md](frontend/CLAUDE.md)** — Vue 3 + Vite SPA (TypeScript, Feature-Sliced Design, PrimeVue, TanStack Query).

There are also five human-facing docs at the root, kept in sync with the code: [README.md](README.md) (overview + doc index), [ARCHITECTURE.md](ARCHITECTURE.md), [CONTRIBUTING.md](CONTRIBUTING.md), [DEPLOYMENT.md](DEPLOYMENT.md) (env-var reference table), [SECURITY.md](SECURITY.md).

`<Codigoactivo/>` is a nonprofit association's website (León, since 2018). Public side: home with a mini game, events, announcements, resources, partners, member registration (adults can register dependent minors), plus the full account lifecycle — login, OTP account verification, forgot/reset password, self-service account page. Admin back-office: ten sections (events, event detail/roster/printable badges, announcements, resources, partners, users, catalogs, dashboard with charts).

## Hard rules

- **All user-facing text goes through Vue I18n** — never hardcode string literals in components. Every UI string is a key in `frontend/src/shared/i18n/locales/es.ts` (rendered with `$t`/`t`); PrimeVue's own built-in strings live separately in `frontend/src/shared/i18n/primevue-locale.ts`. The app is **Spanish-only** for now (single `es` locale); adding a second language is a drop-in `en.ts`. See `frontend/CLAUDE.md`.
- **Never commit secrets.** Runtime config is flat env vars: the git-ignored root `.env` (backend, consumed by Docker Compose) and `frontend/.env.local` (frontend); templates are `.env.example`. There are **no** `dotnet user-secrets` and **no** `ConnectionStrings` section anymore.
- **A change that crosses the API boundary must be made on both sides in the same pass** (see the pipeline below).
- **Keep the checks green — but nothing enforces them for you.** There is no CI (`.github/workflows` is empty and untracked) and no git hooks, so every gate below is manual:
  - Backend: analyzers (Meziantou + Sonar, `AnalysisLevel=latest-Recommended`, `EnforceCodeStyleInBuild`) report **warnings, not errors** — `dotnet build` succeeds with violations present. The zero-warning state is a self-imposed convention; treat a new warning as a failure.
  - Frontend: three independent checks that "lint" collapses — `npm run lint` (ESLint), `npm run lint:fsd` (Steiger, the FSD layer/import rules ESLint does not check), and `npm run build` (runs `vue-tsc`, the only genuinely build-blocking one).
  - Fix the code, never disable the rule.
- **Conventional Commits** (`feat:`, `fix:`, `chore:`, `refactor:`, `test:`) — see [CONTRIBUTING.md](CONTRIBUTING.md).
- **Update the docs in the same pass** when architecture, tech stack, testing conventions, security posture, or the deployment/config surface changes — that includes these `CLAUDE.md` files.

## Quick start (local, without Docker)

```bash
# Backend — needs a reachable PostgreSQL; the connection is built from POSTGRES_* env vars
# (defaults localhost:5432, db/user "codigoactivo", empty password). See backend/CLAUDE.md.
docker compose up -d db                                      # publishes 127.0.0.1:5432 via the override
cd backend && dotnet run --project src/CodigoActivo.API      # http://localhost:5150, Swagger at /swagger

# Frontend
cd frontend && npm ci && npm run dev                         # http://localhost:5173, proxies /api to the backend
```

- **A bare `dotnet run` does not read the root `.env`** — that file is Docker-Compose-only. `POSTGRES_*`/`SMTP_*` must be real environment variables. The "empty password" default only works against a trust-auth Postgres; the compose-provisioned DB needs `POSTGRES_PASSWORD`.
- Anything beyond `dotnet run` (build, test) must name the solution explicitly — `dotnet build CodigoActivo.slnx` — because `backend/` also contains `docker-compose.dcproj` (bare `dotnet build` fails MSB1011).
- The Vite proxy target defaults to `https://localhost:5001`; point it at the local API with `VITE_API_PROXY_TARGET=http://localhost:5150` in `frontend/.env.local` (copy `frontend/.env.example`). The dev server also proxies the root `/sitemap.xml` and `/robots.txt`, mirroring nginx.
- Prefer `npm ci` over `npm install`: the local npm rewrites `package-lock.json` in a way that breaks the Docker build's `npm ci`.
- On startup the backend **always** applies EF migrations and seeds catalog data (no toggle).

## Docker (the deploy path)

`docker-compose.yml` (project name `codigoactivo`) defines the whole stack:

- `db` — postgres:17-alpine on the **internal** `backend` network, `no-new-privileges`.
- `api` — built from `backend/src/CodigoActivo.API/Dockerfile`, listens on `:8080`, on both networks.
- `web` — nginx from `frontend/Dockerfile`, published on `${HTTP_PORT:-8080}`, `frontend` network only, reverse-proxying `/api` — plus the root `/sitemap.xml` and `/robots.txt` — → `api:8080`.

`api` and `web` are hardened **identically**: `read_only`, `tmpfs: /tmp`, `cap_drop: ALL`, `no-new-privileges`, and both run non-root (api as uid 1654; web on `nginx-unprivileged` as uid 101). Startup is a healthcheck chain: db healthy → api healthy (`HEALTHCHECK` hits `/api/auth/csrf`) → web.

Four named volumes: `db-data`, `api-files`, `api-logs`, `api-dataprotection` — **losing `api-dataprotection` invalidates every session cookie**.

The nginx config (`frontend/docker/default.conf` + `security-headers.conf` + `proxy-api.conf`) does much more than proxy: a strict CSP and security-header set, per-IP rate limits with a separate strict zone for the auth/credential endpoints, realip from `X-Forwarded-For`, a local `/healthz`, immutable `/assets/` caching, and a method allowlist.

All config comes from the root `.env` (copy `.env.example`; set at least `POSTGRES_PASSWORD`, and `APP_BASE_URL` — it defaults to `https://localhost`, which produces a useless sitemap; `SMTP_*` is required whenever `ACCOUNT_VERIFICATION_REQUIRED` is true). Full table in [DEPLOYMENT.md](DEPLOYMENT.md).

- Local/debug: `docker compose up` — auto-merges `docker-compose.override.yml` (Development env, API on `:5150`, db on `127.0.0.1:5432`, `backend` network no longer internal, hardening relaxed; also drives Visual Studio F5 via `backend/docker-compose.dcproj`).
- Production: `docker compose -f docker-compose.yml up -d --build` — **must** pass `-f docker-compose.yml` to exclude the dev override.

## How the two apps are contractually linked

A change crossing the API boundary follows this pipeline — keep every step in sync:

1. **DTOs / endpoints** change in the backend (`Application/DTOs`, controllers). Records suffixed `...Request`/`...Response` define the wire shape; enums serialize as strings.
2. **`frontend/swagger.json`** is the committed contract, refreshed **manually** from the running backend's Development-only Swagger endpoint. Nothing regenerates or diffs it automatically, so backend/contract drift is silent until someone reruns the refresh.
3. **`npm run api:generate`** (Orval) regenerates `frontend/src/shared/api/generated/` from that committed file. Generated files are never hand-edited; entities wrap the generated request functions with their own mappers and TanStack Query composables.
4. **Errors**: the backend returns `ApiErrorResponse` with a string `ErrorCode` enum (`backend/src/CodigoActivo.Domain/Common/ErrorCode.cs`); the frontend maps each code to a Spanish message under the **`errors.*` namespace of `frontend/src/shared/i18n/locales/es.ts`**, resolved by `getErrorMessage()` in `frontend/src/shared/lib/api-error.ts` (unknown codes fall back to `errors.generic`). A new failure mode = new `ErrorCode` member + `return Error.<Kind>(ErrorCode.X)` in the service + the Spanish key.
5. **Auth**: session cookie (`credentials: 'include'`) + CSRF token from `GET /api/auth/csrf` sent as `X-CSRF-TOKEN` on unsafe methods — the frontend's `http-client.ts` (which transparently retries once on `InvalidCsrfToken`) and the backend's `CsrfValidationMiddleware` implement the two halves.
6. **Authorization is deny-by-default**: `Program.cs` sets `FallbackPolicy = RequireAuthenticatedUser()`, so an action with no explicit attribute returns 401. **A new public endpoint needs an explicit `[AllowAnonymous]`.** Beyond that, authorization is a boolean admin flag, not roles (`[AllowOnlyAdmin]` / `[AllowOnlySelf]`).

## Testing

Two backend projects under `backend/tests/`: `CodigoActivo.UnitTests` and `CodigoActivo.IntegrationTests`. Run with `dotnet test CodigoActivo.slnx` from `backend/`.

- Integration tests are **Testcontainers-based**: a throwaway `postgres:17-alpine` started once per assembly, migrated, then truncated + reseeded per test (parallelization disabled). **A running Docker daemon is required to run the suite**; no `POSTGRES_*` env vars and no pre-created database are. Escape hatch: `CODIGOACTIVO_TEST_DB_CONNECTION`. There is no EF Core InMemory anywhere.
- **The frontend has no automated test suite at all** — rely on `npm run typecheck` and the lint commands.

## Repository-wide notes

- **Backend caching** is a repo-wide contract, not a backend detail: two in-memory tag-based layers (`HybridCache` over service reads, `OutputCache` over anonymous public GETs) live in `backend/src/CodigoActivo.Application/Caching/` + `backend/src/CodigoActivo.API/Caching/HttpCacheInvalidator.cs`. **The frontend deliberately never HTTP-caches `/api`** because the server absorbs that load — see `frontend/CLAUDE.md`. Every write must invalidate the right tags. (The one exception to "clients never cache": the SEO endpoints send `Cache-Control: public, max-age=1h`.)
- **Demo mode** is backend-only and **off by default**: set the flat env var `DEMO_MODE=true` (in `.env`/compose, read via `config.GetValue("DEMO_MODE", …)`) to seed a full realistic dataset at startup; flipping it back to `false` purges that data on the next startup. It is not an `appsettings.json` key, and the frontend has no demo awareness. **It seeds a well-known password (`Demo1234!`) and must never be enabled in a real deployment** — see [SECURITY.md](SECURITY.md).
- License: GPL-3.0 (`LICENSE`).
