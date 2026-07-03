<p align="center">
<img alt=".NET" src="https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
<img alt="Vue" src="https://img.shields.io/badge/Vue_3-4FC08D?style=for-the-badge&logo=vuedotjs&logoColor=white" />
<img alt="TypeScript" src="https://img.shields.io/badge/TypeScript-3178C6?style=for-the-badge&logo=typescript&logoColor=white" />
<img alt="PostgreSQL" src="https://img.shields.io/badge/PostgreSQL-4169E1?style=for-the-badge&logo=postgresql&logoColor=white" />
<img alt="Vite" src="https://img.shields.io/badge/Vite-646CFF?style=for-the-badge&logo=vite&logoColor=white" />
<img alt="License" src="https://img.shields.io/badge/GPL--3.0-red?style=for-the-badge" />
</p>

# 🌐 &lt;Codigoactivo/&gt;

**Source code of the official website of `<Codigoactivo/>`, a nonprofit association.**

## 📖 Overview

This repository holds a single full-stack web app with two clearly separated surfaces:

- A **public-facing site** where visitors and members discover the association's events and
  activities, read its announcements and resources, meet its partners, and sign up — adults
  can register dependent minors under their own account.
- An **admin back-office** where the association's team manages members, publishes content,
  configures catalogs, and reviews who is assigned to which activity and in which role.

## 🧰 Tech Stack

| Layer        | Technologies                                                                            |
| ------------ | --------------------------------------------------------------------------------------- |
| **Backend**  | ASP.NET Core (.NET 10) · EF Core · PostgreSQL · Argon2id · Swashbuckle (OpenAPI)          |
| **Frontend** | Vue 3 · Vite · TypeScript · PrimeVue · TanStack Query · TipTap · Orval (generated REST client) |
| **Quality**  | Roslynator analyzers (build-enforced) · ESLint · Prettier · `vue-tsc` typecheck · Steiger (FSD lint) |

## 🏗️ Architecture

Two independent apps live side by side, each with its own conventions.

```
CodigoActivo/
├── backend/    ASP.NET Core Web API
└── frontend/   Vue 3 + Vite SPA
```

### Backend

| Project              | Responsibility                                                                  |
| -------------------- | ------------------------------------------------------------------------------- |
| **Domain**           | Entities, per-entity repository interfaces, `Result`/`Error`. No dependencies.  |
| **Application**      | Services (business logic), DTOs, entity→DTO mapping. Depends on Domain only.     |
| **Infrastructure**   | EF Core `DbContext`, repository implementations, Argon2id hashing, file storage. |
| **Composition**      | The single place dependency injection is wired together.                        |
| **API**              | Controllers, middleware, auth attributes. References **Composition** only.       |


### Frontend

| Layer             | Responsibility                                                            |
| ----------------- | ------------------------------------------------------------------------- |
| **`src/app`**     | Bootstrap: providers, centralized router, layouts                         |
| **`src/pages`**   | One slice per route (public pages + admin under `pages/admin/`)           |
| **`src/widgets`** | Composite cross-page UI blocks                                            |
| **`src/features`**| User interactions (auth, register, account, admin `manage-*` CRUD)        |
| **`src/entities`**| Business entities — `model` (types, reactive session state), `api` (requests), `ui` (cards) |
| **`src/shared`**  | Reusable base: API client, UI kit, lib helpers, config                    |

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
npm run lint:fsd     # Steiger — enforce Feature-Sliced Design rules
npm run api:generate # regenerate the typed API client from swagger.json (Orval)
```

## 📝 License

Released under the [GNU General Public License v3.0](LICENSE).