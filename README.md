<p align="center">
  <img
    width="100%"
    alt="Código Activo"
    src="https://capsule-render.vercel.app/api?type=waving&height=220&color=0:f9a320,50:ff6b5e,100:2dd4d9&text=C%C3%B3digo%20Activo&fontColor=ffffff&fontSize=52&fontAlign=50&fontAlignY=40&descAlignY=62&animation=fadeIn"
  />
</p>
<p align="center">
  <img alt=".NET" src="https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img alt="Vue" src="https://img.shields.io/badge/Vue_3-4FC08D?style=for-the-badge&logo=vuedotjs&logoColor=white" />
  <img alt="TypeScript" src="https://img.shields.io/badge/TypeScript-3178C6?style=for-the-badge&logo=typescript&logoColor=white" />
  <img alt="PostgreSQL" src="https://img.shields.io/badge/PostgreSQL_18-4169E1?style=for-the-badge&logo=postgresql&logoColor=white" />
  <img alt="Docker" src="https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white" />
  <img alt="License" src="https://img.shields.io/badge/GPL--3.0-red?style=for-the-badge&logo=gnu&logoColor=white" />
</p>

<p align="center">
  <img alt="API Release" src="https://img.shields.io/github/v/tag/hidden-space-xyz/CodigoActivo?filter=v*-API&style=for-the-badge&label=API&color=2EA44F&logo=github&logoColor=white" />
  <img alt="UI Release" src="https://img.shields.io/github/v/tag/hidden-space-xyz/CodigoActivo?filter=v*-UI&style=for-the-badge&label=UI&color=2EA44F&logo=github&logoColor=white" />
  <img alt="CI" src="https://img.shields.io/github/actions/workflow/status/hidden-space-xyz/CodigoActivo/ci.yml?style=for-the-badge&label=CI&logo=githubactions&logoColor=white" />
  <img alt="CodeQL" src="https://img.shields.io/github/actions/workflow/status/hidden-space-xyz/CodigoActivo/codeql.yml?style=for-the-badge&label=CodeQL&logo=githubactions&logoColor=white" />
</p>

# 🌐 &lt;Codigoactivo/&gt;

Official website and management platform for `<Codigoactivo/>`, a nonprofit association in León that
introduces young people to programming and computational thinking through free, practical activities.

## What the application provides

- A public site for events, announcements, resources and information about the association.
- Registration for adults and dependent minors, optional email verification, password recovery and a
  self-service account area.
- Activity enrollment, participation history, event ratings and downloadable participation certificates.
- An administration area for content, events, activities, attendees, users, catalogs, reports and email.
- A Spanish interface whose user-facing copy is managed through Vue I18n.

## Technology

| Area     | Main technologies                                                                        |
| -------- | ---------------------------------------------------------------------------------------- |
| Backend  | ASP.NET Core on .NET 10, EF Core, PostgreSQL 18, Argon2id, MailKit, OpenAPI              |
| Frontend | Vue 3, Vite, TypeScript, Element Plus, TanStack Query, Vue I18n, TipTap, Chart.js, Orval |
| Quality  | .NET analyzers, CSharpier, ESLint, Stylelint, Steiger, Knip, Prettier, `vue-tsc`         |
| Tests    | xUnit v3, AwesomeAssertions, NSubstitute, Testcontainers, Vitest, Vue Test Utils, MSW    |
| Runtime  | Docker Compose and unprivileged nginx                                                    |

## Repository

```text
CodigoActivo/
├── backend/    ASP.NET Core API organized in five projects
└── frontend/   Vue single-page application organized with Feature-Sliced Design
```

The browser always uses one origin. In development Vite proxies `/api`; in the container stack nginx serves
the SPA and proxies API traffic. See [ARCHITECTURE.md](ARCHITECTURE.md) for the design and boundaries.

## Quick start with Docker

### Local development stack

From a clone, Docker Compose automatically merges `docker-compose.override.yml`. Create the environment file
and choose one email setup: configure SMTP, or set `ACCOUNT_VERIFICATION_REQUIRED=false` for local work.

```bash
cp .env.example .env
# Set POSTGRES_PASSWORD, BOOTSTRAP_ADMIN_EMAIL and BOOTSTRAP_ADMIN_PASSWORD.
# Configure SMTP_* or set ACCOUNT_VERIFICATION_REQUIRED=false.
docker compose up --build
```

The SPA is available at <http://localhost:8080>, the API at <http://localhost:5150>, and Swagger at
<http://localhost:5150/swagger>. The development overlay also publishes PostgreSQL on port `5432`.

> [!WARNING]
> The development overlay publishes ports on all host interfaces and relaxes API container hardening. Do not
> use it for production.

### Production stack

The base Compose file pulls released images from GHCR and does not depend on the repository:

```bash
curl -LO https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/docker-compose.yml
curl -Lo .env https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/.env.example
# Replace every required or placeholder value in .env.
docker compose up -d
```

Production publishes plain HTTP on host port `8080` on all interfaces. Put it behind a TLS reverse proxy and
restrict direct access with the host firewall or an equivalent network policy. The API rejects unsafe
production configuration. Follow [DEPLOYMENT.md](DEPLOYMENT.md) before exposing the service.

## Run the applications directly

Use Docker only for PostgreSQL, then run the API and frontend with hot reload:

```bash
cp .env.example .env
# Set POSTGRES_PASSWORD in .env for the db container.
docker compose up -d db

# Export real process variables; dotnet run does not read the root .env.
export POSTGRES_PASSWORD=...
export ACCOUNT_VERIFICATION_REQUIRED=false
export BOOTSTRAP_ADMIN_EMAIL=admin@example.test
export BOOTSTRAP_ADMIN_PASSWORD=...
cd backend
dotnet run --project src/CodigoActivo.API

cd ../frontend
npm ci
cp .env.example .env.local
# Set VITE_API_PROXY_TARGET=http://localhost:5150.
npm run dev
```

PowerShell uses `$env:NAME="value"` instead of `export NAME=value`. The complete setup, command and testing
workflow is in [CONTRIBUTING.md](CONTRIBUTING.md).

## Documentation

| Document                           | Purpose                                                      |
| ---------------------------------- | ------------------------------------------------------------ |
| [ARCHITECTURE.md](ARCHITECTURE.md) | System structure, dependency rules and API contract          |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Development setup, checks, migrations and contribution rules |
| [DEPLOYMENT.md](DEPLOYMENT.md)     | Production topology, configuration, releases and backups     |
| [SECURITY.md](SECURITY.md)         | Vulnerability reporting and the security model               |
| [CLAUDE.md](CLAUDE.md)             | Concise repository guidance for coding agents                |

## License

Released under the [GNU General Public License v3.0](LICENSE).

<p align="center">
  <img
    width="100%"
    alt=""
    src="https://capsule-render.vercel.app/api?type=waving&section=footer&height=140&color=0:2dd4d9,50:ff6b5e,100:f9a320&animation=twinkling"
  />
</p>
