<div align="center">

# Código Activo

**Management platform for nonprofit associations**

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Vue](https://img.shields.io/badge/Vue-3-4FC08D?logo=vuedotjs&logoColor=white)](https://vuejs.org/)
[![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Vite](https://img.shields.io/badge/Vite-646CFF?logo=vite&logoColor=white)](https://vite.dev/)

</div>

---

## 📖 Overview

**Código Activo** is a full-stack web platform for running the day-to-day of a community
association. It is split into two cleanly separated surfaces served by a single backend:

- A **public-facing site** where visitors browse events and activities, read announcements
  and resources, meet partners, and register as members (adults can register dependent
  minors under their own account).
- An **admin back-office** where staff manage users, publish content, configure catalogs,
  and review who is assigned to which activity and in which role.

## 🧰 Tech Stack

| Layer        | Technologies                                                                            |
| ------------ | --------------------------------------------------------------------------------------- |
| **Backend**  | ASP.NET Core (.NET 10) · EF Core · PostgreSQL · Argon2id · Swashbuckle (OpenAPI)          |
| **Frontend** | Vue 3 · Vite · TypeScript · PrimeVue · Pinia · TanStack Query · TipTap · Orval           |
| **Quality**  | Roslynator analyzers (build-enforced) · ESLint · Prettier · `vue-tsc` typecheck          |

## 🏗️ Architecture

Two independent apps live side by side, each with its own conventions.

```
CodigoActivo/
├── backend/    ASP.NET Core Web API — Clean Architecture, 5 projects
└── frontend/   Vue 3 + Vite SPA — generated API client
```

### Backend — strict dependency direction

```
API ──> Composition ──> Application ──> Domain
                    └──> Infrastructure ─> Domain
```

| Project              | Responsibility                                                                  |
| -------------------- | ------------------------------------------------------------------------------- |
| **Domain**           | Entities, per-entity repository interfaces, `Result`/`Error`. No dependencies.  |
| **Application**      | Services (business logic), DTOs, entity→DTO mapping. Depends on Domain only.     |
| **Infrastructure**   | EF Core `DbContext`, repository implementations, Argon2id hashing, file storage. |
| **Composition**      | The single place dependency injection is wired together.                        |
| **API**              | Controllers, middleware, auth attributes. References **Composition** only.       |


### Frontend — two areas, two styles

| Area              | Purpose            | Style                                                                |
| ----------------- | ------------------ | ------------------------------------------------------------------- |
| **`src/modules`** | Public-facing site | Clean Architecture per module (`domain` / `application` / `infrastructure` / `presentation`). |
| **`src/features`**| Admin area         | Deliberately flat — `XPage.vue` + `XFormDialog.vue` + `useX.ts` calling generated query hooks. |
| **`src/shared`**  | Cross-cutting      | Generated API client, fetch mutator (cookie + CSRF), env config.    |
| **`src/app`**     | Bootstrap          | `main.ts`, providers (Pinia, PrimeVue, TanStack Query), router.     |

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- [PostgreSQL](https://www.postgresql.org/) running locally

### 1. Backend

```bash
cd backend

# Point the API at your PostgreSQL instance (edit appsettings.json or use a secret/env var):
#   ConnectionStrings:Default = Host=localhost;Port=5432;Database=codigoactivo;Username=postgres;Password=...

dotnet run --project CodigoActivo.API
```

On startup the API **applies migrations and seeds the catalogs automatically** (toggle with
`Database:MigrateOnStartup` / `Database:SeedOnStartup`). It then listens on:

- API · <http://localhost:5150> and <https://localhost:7039>
- Swagger UI · <https://localhost:7039/swagger> *(Development only)*

### 2. Frontend

```bash
cd frontend
npm install

# Tell Vite which backend to proxy /api to (defaults to https://localhost:5001):
cp .env.example .env.local
#   VITE_API_PROXY_TARGET=https://localhost:7039

npm run dev
```

The SPA is served at <http://localhost:5173>.

## ⚙️ Configuration

### Backend — `backend/CodigoActivo.API/appsettings.json`

| Key                          | Description                                              | Default                       |
| ---------------------------- | ------------------------------------------------------- | ----------------------------- |
| `ConnectionStrings:Default`  | PostgreSQL connection string                            | local `codigoactivo` database |
| `Database:MigrateOnStartup`  | Apply EF Core migrations on boot                        | `true`                        |
| `Database:SeedOnStartup`     | Seed catalog data on boot                               | `true`                        |
| `FileStorage:RootPath`       | Local directory for uploaded files                      | `files`                       |
| `FileStorage:MaxSizeBytes`   | Max upload size                                          | `5 MiB`                       |
| `Auth:CookieName` · `SameSite` · `ExpireHours` | Session cookie settings               | `CodigoActivo.Session` · `Lax` · `8` |
| `Cors:AllowedOrigins`        | Origins allowed to call the API with credentials        | `localhost:5173`, …           |

### Frontend — `frontend/.env.local`

| Variable                 | Description                                          | Default                  |
| ------------------------ | --------------------------------------------------- | ------------------------ |
| `VITE_API_PROXY_TARGET`  | Backend the Vite dev server proxies `/api` to       | `https://localhost:5001` |
| `VITE_API_BASE_URL`      | Base URL for the API client (empty = same-origin)   | *(empty)*                |

## 🛠️ Development

### Common commands

**Backend** (run from `backend/`)

```bash
dotnet build                                   # build + run analyzers (style violations fail the build)
dotnet run --project CodigoActivo.API          # run the API

# Add a migration (applied automatically on next startup):
dotnet ef migrations add <Name> \
  --project CodigoActivo.Infrastructure \
  --startup-project CodigoActivo.API
```

**Frontend** (run from `frontend/`)

```bash
npm run dev          # dev server with HMR
npm run build        # typecheck (vue-tsc) + production build
npm run lint         # ESLint   ·   npm run format → Prettier
npm run api:generate # regenerate the typed API client from swagger.json (Orval)
```