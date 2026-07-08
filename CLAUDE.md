# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Website of `<Codigoactivo/>`, a nonprofit association: a public site (events, announcements, resources, member registration — adults can register dependent minors) plus an admin back-office. **All user-facing text is Spanish; there is no i18n layer.**

Two independent apps, each with its own `CLAUDE.md` covering commands and conventions in depth — **read the one for the folder you're working in**:

- **[backend/CLAUDE.md](backend/CLAUDE.md)** — ASP.NET Core Web API (.NET 10, EF Core + PostgreSQL, 5-project clean architecture).
- **[frontend/CLAUDE.md](frontend/CLAUDE.md)** — Vue 3 + Vite SPA (TypeScript, Feature-Sliced Design, PrimeVue, TanStack Query).

## Quick start

```bash
# Backend (needs PostgreSQL + user secrets, see backend/CLAUDE.md)
cd backend && dotnet run --project src/CodigoActivo.API    # http://localhost:5150, Swagger at /swagger

# Frontend
cd frontend && npm install && npm run dev                  # http://localhost:5173, proxies /api to the backend
```

Set `VITE_API_PROXY_TARGET=http://localhost:5150` in `frontend/.env.local` so the Vite proxy points at the local API. On startup the backend applies EF migrations and seeds catalog data automatically.

## How the two apps are contractually linked

Changes that cross the API boundary follow this pipeline — keep every step in sync:

1. **DTOs / endpoints** change in the backend (`Application/DTOs`, controllers). DTO records suffixed `...Request`/`...Response` define the wire shape; enums serialize as strings.
2. **`frontend/swagger.json`** is the committed contract, refreshed from the running backend's Swagger endpoint (Development-only).
3. **`npm run api:generate`** (Orval) regenerates `frontend/src/shared/api/generated/` from it. Generated files are never hand-edited; entities wrap the generated request functions with their own mappers and TanStack Query composables.
4. **Error handling**: the backend returns `ApiErrorResponse` with a string `ErrorCode` enum (`backend/src/CodigoActivo.Domain/Common/ErrorCode.cs`); the frontend maps each code to a Spanish message in `frontend/src/shared/api/error-messages.ts`. Adding a failure mode means touching both files.
5. **Auth**: session cookie (`credentials: 'include'`) + CSRF token from `GET /api/auth/csrf` sent as `X-CSRF-TOKEN` on unsafe methods. The frontend's `http-client.ts` and the backend's `CsrfValidationMiddleware` implement the two halves. Authorization is a boolean admin flag, not roles.

## Repository-wide notes

- **Demo mode**: backend config key `DemoMode` (currently `true` in `appsettings.json`) seeds a full realistic demo dataset at startup and removes it when flipped off. It is backend-only; the frontend has no demo awareness.
- Credentials are never committed — backend uses dotnet user-secrets, frontend uses `.env.local`.
- Both sides enforce style at build/lint time: backend via Roslyn analyzers (Meziantou + Sonar, style violations fail `dotnet build`) and CSharpier; frontend via ESLint + Prettier + `vue-tsc` (very strict tsconfig) + Steiger (FSD layer rules).
- License: GPL-3.0.
