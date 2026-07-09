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
| **Backend**  | ASP.NET Core (.NET 10) · EF Core · PostgreSQL · Argon2id · Serilog · Swashbuckle (OpenAPI)  |
| **Frontend** | Vue 3 · Vite · TypeScript · PrimeVue · TanStack Query · TipTap · Orval (generated REST client) |
| **Quality**  | Meziantou.Analyzer · SonarAnalyzer (build-enforced) · ESLint · Prettier · `vue-tsc` typecheck · Steiger (FSD lint) |

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
| **`src/features`** | User interactions (auth, register, account, admin `manage-*` CRUD)        |
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

# One-time setup: store your PostgreSQL credentials in user secrets
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=codigoactivo;Username=postgres;Password=..." --project src/CodigoActivo.API

dotnet run --project src/CodigoActivo.API
```

On startup the API **applies migrations and seeds the catalogs automatically** (toggle with
`Database:MigrateOnStartup` / `Database:SeedOnStartup`). It then listens on:

- API · <http://localhost:5150> (add `--launch-profile https` to also listen on <https://localhost:7039>)
- Swagger UI · <http://localhost:5150/swagger> *(Development only)*

### 2. Frontend

```bash
cd frontend
npm install

# Tell Vite which backend to proxy /api to (defaults to https://localhost:5001):
cp .env.example .env.local
#   VITE_API_PROXY_TARGET=http://localhost:5150

npm run dev
```

The SPA is served at <http://localhost:5173>.

## ⚙️ Configuration

### Backend — `backend/src/CodigoActivo.API/appsettings.json`

All settings live in the single `appsettings.json`. Credentials are never committed: they are stored
per developer with [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(loaded automatically in the `Development` environment):

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=...;Password=..." --project src/CodigoActivo.API
dotnet user-secrets set "Smtp:Username" "..." --project src/CodigoActivo.API
dotnet user-secrets set "Smtp:Password" "..." --project src/CodigoActivo.API
```

| Key                          | Description                                              | Default                       |
| ---------------------------- | ------------------------------------------------------- | ----------------------------- |
| `App:TimeZone`               | IANA time zone the app operates in                      | `Europe/Madrid`               |
| `App:BaseUrl`                | Public base URL used in outgoing links                  | `http://localhost:5173`       |
| `ConnectionStrings:Default`  | PostgreSQL connection string                            | *(user secrets)*              |
| `Database:MigrateOnStartup`  | Apply EF Core migrations on boot                        | `true`                        |
| `Database:SeedOnStartup`     | Seed catalog data on boot                               | `true`                        |
| `FileStorage:RootPath`       | Local directory for uploaded files                      | `files`                       |
| `FileStorage:MaxSizeBytes`   | Max upload size                                          | `10 MiB`                      |
| `Auth:CookieName` · `SameSite` · `ExpireHours` | Session cookie settings               | `CodigoActivo.Session` · `Lax` · `8` |
| `AccountVerification:Required` | Require email (OTP) verification before login. Enable it in production | `false` |
| `AccountVerification:OtpLifetimeMinutes` · `ResendCooldownSeconds` | OTP policy          | `15` · `60`                   |
| `Smtp:Host` · `Port` · `Security` | SMTP server used to send verification emails (`Security`: `StartTls`, `SslOnConnect`, `None`, `Auto`) | `localhost` · `587` · `StartTls` |
| `Smtp:Username` · `Password`  | SMTP credentials                                        | *(user secrets)*              |
| `Smtp:FromAddress` · `FromName` | Sender identity for outgoing email                    | `` · `Código Activo` |

### Frontend — `frontend/.env.local`

| Variable                 | Description                                          | Default                  |
| ------------------------ | --------------------------------------------------- | ------------------------ |
| `VITE_API_PROXY_TARGET`  | Backend the Vite dev server proxies `/api` to       | `https://localhost:5001` |

## 🛠️ Development

### Common commands

**Backend** (run from `backend/`)

```bash
dotnet build                                   # build + run analyzers (style violations fail the build)
dotnet run --project src/CodigoActivo.API      # run the API

# Add a migration (requires the EF tool: dotnet tool install -g dotnet-ef).
# Migrations are applied automatically on next startup:
dotnet ef migrations add <Name> \
  --project src/CodigoActivo.Infrastructure \
  --startup-project src/CodigoActivo.API
```

**Frontend** (run from `frontend/`)

```bash
npm run dev          # dev server with HMR
npm run build        # typecheck (vue-tsc) + production build
npm run lint         # ESLint   ·   npm run format → Prettier
npm run lint:fsd     # Steiger — enforce Feature-Sliced Design rules
npm run api:generate # regenerate the typed API client from swagger.json (Orval)
```

## 🐳 Running with Docker

The repository ships a Compose stack (`db` + `api` + `web`). Copy `.env.example` to `.env`
and set at least `POSTGRES_PASSWORD` first.

- **Local development / debugging** — `docker compose up`. This automatically layers
  `docker-compose.override.yml` on top of the base stack: it switches the API to the
  `Development` environment, publishes it on <http://localhost:5150> (Swagger at `/swagger`),
  and relaxes the container hardening so the debugger can attach. **Visual Studio** picks up
  the same override on **F5**: set `docker-compose` as the startup project to build the API in
  `Debug`, mount the debugger, and step through the containerized backend.

- **Production** — deploy the hardened base stack only, explicitly excluding the dev override:

  ```bash
  docker compose -f docker-compose.yml up -d --build
  ```

  The base `docker-compose.yml` runs the API as a non-root user with a read-only filesystem,
  all capabilities dropped, and `ASPNETCORE_ENVIRONMENT=Production`. Running a bare
  `docker compose up` on a server would pull in the dev override — always pass
  `-f docker-compose.yml`.

## 📝 License

Released under the [GNU General Public License v3.0](LICENSE).