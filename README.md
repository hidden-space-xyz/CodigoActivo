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
introduces young people to programming and computational thinking through free, practical activities. It
provides a public site for events and resources, self-service registration with mandatory two-factor login
(emailed code or authenticator app), activity enrollment and participation history, event ratings and
certificates, and an administration area for content, events, users and email. The interface is Spanish,
managed through Vue I18n.

## Technology

| Area     | Main technologies                                                                        |
| -------- | ---------------------------------------------------------------------------------------- |
| Backend  | ASP.NET Core on .NET 10, EF Core, PostgreSQL 18, Argon2id, Otp.NET, MailKit, OpenAPI     |
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

From a clone, Compose automatically merges `docker-compose.override.yml`, which delivers every email
(including login codes) to a bundled Mailpit catcher, so no SMTP server is needed locally.

```bash
cp .env.example .env
# Set POSTGRES_PASSWORD, BOOTSTRAP_ADMIN_EMAIL and BOOTSTRAP_ADMIN_PASSWORD.
docker compose up --build
```

The SPA is at <http://localhost:8080>, the API at <http://localhost:5150>, Swagger at
<http://localhost:5150/swagger> and the caught mail at <http://localhost:8025>. The overlay also publishes
PostgreSQL on port `5432`.

> [!WARNING]
> The development overlay publishes ports on all host interfaces and relaxes API container hardening. Do not
> use it for production.

### Production stack

The base Compose file pulls released images from GHCR and does not depend on a repository clone:

```bash
curl -LO https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/docker-compose.yml
curl -Lo .env https://raw.githubusercontent.com/hidden-space-xyz/CodigoActivo/master/.env.example
# Replace every required or placeholder value in .env.
docker compose up -d
```

Follow [DEPLOYMENT.md#first-production-start](DEPLOYMENT.md#first-production-start) before exposing the
service.

To run the applications directly with hot reload instead of full containers, see
[CONTRIBUTING.md](CONTRIBUTING.md#local-setup).

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
