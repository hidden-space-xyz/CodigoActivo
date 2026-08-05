# CLAUDE.md

Guidance for Claude Code in this repository. **Each app has its own `CLAUDE.md` — read the one for the folder you are editing.**

- **[backend/CLAUDE.md](backend/CLAUDE.md)** — ASP.NET Core Web API (.NET 10, EF Core + PostgreSQL, 5-project clean architecture).
- **[frontend/CLAUDE.md](frontend/CLAUDE.md)** — Vue 3 + Vite SPA (TypeScript, Feature-Sliced Design, PrimeVue, TanStack Query).

Five human-facing docs at the root are kept in sync with the code: [README.md](README.md) (overview + doc index), [ARCHITECTURE.md](ARCHITECTURE.md), [CONTRIBUTING.md](CONTRIBUTING.md), [DEPLOYMENT.md](DEPLOYMENT.md) (env-var reference table), [SECURITY.md](SECURITY.md).

## What the app is

`<Codigoactivo/>` is a nonprofit association's website (León, since 2016).

- **Public**: home, events, announcements, resources, partners, member registration (adults may register dependent minors), plus the full account lifecycle — login, OTP verification, forgot/reset password, and a self-service account page (profile, dependent minors, and a participation **history** from which members rate finished events).
- **Admin**: ten routes — dashboard (charts), events, event detail (*Actividades*/*Asistentes*/*Opiniones* tabs), printable badges, printable roster, announcements, resources, partners, users, catalogs.
- **Email**: admins compose plain-text mail with attachments to one person or to everyone matching the current filters, from both the users table and the *Asistentes* tab (both lists also export to CSV). Two further notifications are **automatic**: signing up for an activity acknowledges the request as pending, and an admin's confirm/reject tells the member the outcome — always to the guardian's address when the enrolled person is a dependent minor.

## Hard rules

- **All user-facing text goes through a resource file — never a hardcoded literal.** Frontend: every UI string is a key in `frontend/src/shared/i18n/locales/es.ts` (PrimeVue's own built-ins live in `frontend/src/shared/i18n/primevue-locale.ts`). Backend: every user-facing string is a key in `backend/src/CodigoActivo.Application/Resources/Localization/AppStrings.resx`, read through the `AppStrings` accessor. Both apps are **Spanish-only** for now, but the two halves are *not* equally ready for a second language: the frontend is a genuine drop-in `en.ts`, while the backend is key-organised but **not runtime-switchable** until the container's globalization mode changes — see `backend/CLAUDE.md`. Backend seed catalog text (`DatabaseSeeder`) is deliberately **not** localized: it persists as database rows.
- **Never commit secrets.** Runtime config is flat env vars: the git-ignored root `.env` (backend, consumed by Docker Compose) and `frontend/.env.local`; templates are the `.env.example` files. There are **no** `dotnet user-secrets` and **no** `ConnectionStrings` section.
- **A change that crosses the API boundary must be made on both sides in the same pass** — see the pipeline below.
- **Keep the checks green — but nothing enforces them for you.** There is no CI (`.github/workflows` is empty and untracked) and no git hooks, so every gate is manual: `dotnet build CodigoActivo.slnx` from `backend/` (analyzers report **warnings, never errors**, and re-run on every build — see `backend/CLAUDE.md`), and `npm run lint`, `npm run lint:fsd`, `npm run build` from `frontend/` (three independent checks; only the last is genuinely build-blocking). **Fix the code, never disable the rule.**
- **Conventional Commits** (`feat:`, `fix:`, `chore:`, `refactor:`, `test:`) — see [CONTRIBUTING.md](CONTRIBUTING.md).
- **Update the docs in the same pass** when architecture, tech stack, testing conventions, security posture or the deployment/config surface changes — that includes these `CLAUDE.md` files.

## Quick start (local, without Docker)

```bash
docker compose up -d db                                      # publishes 127.0.0.1:5432 via the override
cd backend && dotnet run --project src/CodigoActivo.API      # http://localhost:5150, Swagger at /swagger
cd frontend && npm ci && npm run dev                         # http://localhost:5173, proxies /api to the backend
```

- **A bare `dotnet run` does not read the root `.env`** — that file is Docker-Compose-only, so `POSTGRES_*`/`SMTP_*` must be real environment variables. The empty-password default only works against a trust-auth Postgres; the compose-provisioned DB needs `POSTGRES_PASSWORD`.
- Anything beyond `dotnet run` (build, test) must name the solution — `dotnet build CodigoActivo.slnx` — because `backend/` also holds `docker-compose.dcproj`, which makes a bare `dotnet build` ambiguous (MSB1011).
- Point Vite at the local API with `VITE_API_PROXY_TARGET=http://localhost:5150` in `frontend/.env.local`; the code fallback is `https://localhost:5001`. The dev server also proxies the root `/sitemap.xml` and `/robots.txt`, mirroring nginx.
- Prefer `npm ci` over `npm install`: the local npm rewrites `package-lock.json` in a way that breaks the Docker build's `npm ci`.
- On startup the backend **always** applies EF migrations and seeds catalog data (no toggle).

## Docker (the deploy path)

`docker-compose.yml` (project name `codigoactivo`) defines the whole stack:

- `db` — postgres:17-alpine on the **internal** `backend` network, `no-new-privileges`.
- `api` — built from `backend/src/CodigoActivo.API/Dockerfile`, listens on `:8080`, on both networks.
- `web` — nginx from `frontend/Dockerfile`, published on `${HTTP_PORT:-8080}`, `frontend` network only, reverse-proxying `/api` — plus the root `/sitemap.xml` and `/robots.txt` — → `api:8080`.

`api` and `web` are hardened **identically**: `read_only`, `tmpfs: /tmp`, `cap_drop: ALL`, `no-new-privileges`, both non-root (api as uid 1654, web on `nginx-unprivileged` as uid 101). Startup is a healthcheck chain: db healthy → api healthy (`HEALTHCHECK` hits `/api/auth/csrf`) → web.

Four named volumes: `db-data`, `api-files`, `api-logs`, `api-dataprotection` — **losing `api-dataprotection` invalidates every session cookie**.

The nginx config (`frontend/docker/`) does much more than proxy: strict CSP and security headers, per-IP rate limits with a separate strict zone for the credential endpoints, realip from `X-Forwarded-For`, a local `/healthz`, immutable `/assets/` caching, a method allowlist. Every one of those is a trap when edited — see `frontend/CLAUDE.md`.

Config comes from the root `.env` (copy `.env.example`; set at least `POSTGRES_PASSWORD` and `APP_BASE_URL` — the latter defaults to `https://localhost`, which produces a useless sitemap; `SMTP_*` is required whenever `ACCOUNT_VERIFICATION_REQUIRED` is true). Full table in [DEPLOYMENT.md](DEPLOYMENT.md).

- Local/debug: `docker compose up` — auto-merges `docker-compose.override.yml` (Development env, API on `:5150`, db on `127.0.0.1:5432`, `backend` network no longer internal, hardening relaxed; also drives Visual Studio F5 via `backend/docker-compose.dcproj`).
- Production: `docker compose -f docker-compose.yml up -d --build` — the `-f` is **mandatory**, it is what excludes the dev override.

## How the two apps are contractually linked

A change crossing the API boundary follows this pipeline — keep every step in sync:

1. **DTOs / endpoints** change in the backend (`Application/DTOs`, controllers). Records suffixed `...Request`/`...Response` define the wire shape; enums serialize as strings.
2. **`frontend/swagger.json`** is the committed contract, refreshed **manually** from the running backend's Development-only Swagger endpoint. Nothing regenerates or diffs it, so contract drift is silent until someone reruns the refresh.
3. **`npm run api:generate`** (Orval) regenerates `frontend/src/shared/api/generated/` from that committed file. Generated files are never hand-edited.
4. **Errors**: the backend returns `ApiErrorResponse` with a string `ErrorCode` enum (`backend/src/CodigoActivo.Domain/Common/ErrorCode.cs`); the frontend maps each code to Spanish copy under the **`errors.*` namespace of `frontend/src/shared/i18n/locales/es.ts`**, resolved by `getErrorMessage()` in `frontend/src/shared/lib/api-error.ts` (unknown codes fall back to `errors.generic`). A new failure mode = new `ErrorCode` member + `return Error.<Kind>(ErrorCode.X)` in the service + the Spanish key.
5. **Auth**: session cookie (`credentials: 'include'`) + a CSRF token from `GET /api/auth/csrf` sent as `X-CSRF-TOKEN` on unsafe methods — the frontend's `http-client.ts` (which transparently retries once on `InvalidCsrfToken`) and the backend's `CsrfValidationMiddleware` are the two halves.
6. **Authorization is deny-by-default**: the fallback policy is `RequireAuthenticatedUser()`, so an action with no explicit attribute returns 401. **A new public endpoint needs an explicit `[AllowAnonymous]`.** Beyond that, authorization is a boolean admin flag, not roles (`[AllowOnlyAdmin]` / `[AllowOnlySelf]`).

## Testing

Two backend projects under `backend/tests/` — `CodigoActivo.UnitTests` and `CodigoActivo.IntegrationTests`. Run both with `dotnet test CodigoActivo.slnx` from `backend/`.

- Integration tests are **Testcontainers-based**: a throwaway `postgres:17-alpine` started once per assembly, migrated, then truncated + reseeded per test. **A running Docker daemon is required**; `POSTGRES_*` env vars and a pre-created database are not. There is no EF Core InMemory anywhere.
- **The frontend has no automated test suite at all** — rely on `npm run typecheck` and the lint commands.

## Repo-wide notes

- **Caching is a two-sided contract.** The backend runs two in-memory tag-based layers (`HybridCache` over service reads, `OutputCache` over anonymous public GETs) and **the frontend deliberately never HTTP-caches `/api`**, because the server absorbs that load. Do not add client caching; do keep tag invalidation correct on every write. Details in both sub-docs. The one exception is the SEO endpoints, which send `Cache-Control: public, max-age=1h`.
- **Outbound email is rate-limited backend-side and the frontend is untouched by it.** One always-on guard at the `IEmailSender` boundary covers every *automatic* send; admin-written mail is exempt at any volume by construction. No API contract changed — a denial is silent everywhere except `resend-verification`, which reuses the existing `OtpResendCooldownActive`. See [SECURITY.md](SECURITY.md) and `backend/CLAUDE.md`.
- **Demo mode** is backend-only and **off by default**: the flat env var `DEMO_MODE=true` seeds a full realistic dataset at startup, and flipping it back to `false` purges it on the next startup. The frontend has no demo awareness. **It seeds a well-known password (`Demo1234!`) and must never be enabled in a real deployment** — see [SECURITY.md](SECURITY.md).
- License: GPL-3.0 (`LICENSE`).
