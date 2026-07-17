# CLAUDE.md

Guidance for Claude Code when working in this repository. **Each app has its own `CLAUDE.md` — read the one for the folder you're editing.**

- **[backend/CLAUDE.md](backend/CLAUDE.md)** — ASP.NET Core Web API (.NET 10, EF Core + PostgreSQL, 5-project clean architecture).
- **[frontend/CLAUDE.md](frontend/CLAUDE.md)** — Vue 3 + Vite SPA (TypeScript, Feature-Sliced Design, PrimeVue, TanStack Query).

`<Codigoactivo/>` is a nonprofit association's website: a public site (events, announcements, resources, member registration — adults can register dependent minors) plus an admin back-office.

## Hard rules

- **All user-facing text is Spanish; there is no i18n layer** — write Spanish string literals directly.
- **Never commit secrets.** Runtime config is flat env vars: the git-ignored root `.env` (backend, consumed by Docker Compose) and `frontend/.env.local` (frontend); templates are `.env.example`. There are **no** `dotnet user-secrets` and **no** `ConnectionStrings` section anymore.
- **A change that crosses the API boundary must be made on both sides in the same pass** (see the pipeline below).
- **Style violations fail the build/lint on both apps** — fix the code, never disable the rule.

## Quick start (local, without Docker)

```bash
# Backend — needs a reachable PostgreSQL; the connection is built from POSTGRES_* env vars
# (defaults localhost:5432, db/user "codigoactivo", empty password). See backend/CLAUDE.md.
cd backend && dotnet run --project src/CodigoActivo.API      # http://localhost:5150, Swagger at /swagger

# Frontend
cd frontend && npm install && npm run dev                    # http://localhost:5173, proxies /api to the backend
```

The Vite proxy target defaults to `https://localhost:5001`; point it at the local API with `VITE_API_PROXY_TARGET=http://localhost:5150` in `frontend/.env.local`. On startup the backend **always** applies EF migrations and seeds catalog data (no toggle).

## Docker (the deploy path)

`docker-compose.yml` defines the whole stack: `db` (postgres:17-alpine, internal network), `api` (built from `backend/src/CodigoActivo.API/Dockerfile`, listens on `:8080`, hardened — read-only, non-root uid 1654, all caps dropped), `web` (nginx from `frontend/Dockerfile`, published on `${HTTP_PORT:-8080}`, reverse-proxying `/api` — plus the root `/sitemap.xml` and `/robots.txt` — → `api:8080`). All config comes from the root `.env` (copy `.env.example`, set at least `POSTGRES_PASSWORD`).

- Local/debug: `docker compose up` — auto-merges `docker-compose.override.yml` (Development env, API on `:5150`, db on `127.0.0.1:5432`, hardening relaxed; also drives Visual Studio F5 via `backend/docker-compose.dcproj`).
- Production: `docker compose -f docker-compose.yml up -d --build` — **must** pass `-f docker-compose.yml` to exclude the dev override.

## How the two apps are contractually linked

A change crossing the API boundary follows this pipeline — keep every step in sync:

1. **DTOs / endpoints** change in the backend (`Application/DTOs`, controllers). Records suffixed `...Request`/`...Response` define the wire shape; enums serialize as strings.
2. **`frontend/swagger.json`** is the committed contract, refreshed from the running backend's Development-only Swagger endpoint.
3. **`npm run api:generate`** (Orval) regenerates `frontend/src/shared/api/generated/`. Generated files are never hand-edited; entities wrap the generated request functions with their own mappers and TanStack Query composables.
4. **Errors**: the backend returns `ApiErrorResponse` with a string `ErrorCode` enum (`backend/src/CodigoActivo.Domain/Common/ErrorCode.cs`); the frontend maps each code to a Spanish message in `frontend/src/shared/api/error-messages.ts`. A new failure mode touches both files.
5. **Auth**: session cookie (`credentials: 'include'`) + CSRF token from `GET /api/auth/csrf` sent as `X-CSRF-TOKEN` on unsafe methods — the frontend's `http-client.ts` and the backend's `CsrfValidationMiddleware` implement the two halves. Authorization is a boolean admin flag, not roles.

## Repository-wide notes

- **Demo mode** is backend-only and **off by default**: set the flat env var `DEMO_MODE=true` (in `.env`/compose, read via `config.GetValue("DEMO_MODE", …)`) to seed a full realistic dataset at startup; flipping it back to `false` purges that data on the next startup. It is not an `appsettings.json` key, and the frontend has no demo awareness.
- License: GPL-3.0 (`LICENSE`).
