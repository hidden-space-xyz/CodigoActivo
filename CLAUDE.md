# Repository guidance for coding agents

`<Codigoactivo/>` is a same-origin web application: a .NET 10 API in `backend/` and a Vue 3/TypeScript SPA in
`frontend/`. Read the document that owns the area you are changing:

- [ARCHITECTURE.md](ARCHITECTURE.md): boundaries, data flow and API contract.
- [CONTRIBUTING.md](CONTRIBUTING.md): local setup, commands and coding conventions.
- [DEPLOYMENT.md](DEPLOYMENT.md): Compose, environment variables, releases and backups.
- [SECURITY.md](SECURITY.md): authentication, trust boundaries and abuse controls.

Update the owning document in the same change when behavior, architecture, configuration or security changes.
Do not copy the same explanation into every file. Documentation must stay short and factual. Do not grow a
document with every change: update the fact that changed, remove what it replaces, and add sections only for
new operational facts.

Backend and frontend conventions and commands live in [backend/CLAUDE.md](backend/CLAUDE.md) and
[frontend/CLAUDE.md](frontend/CLAUDE.md).

## Non-negotiable rules

- Never implement cryptographic or one-time-password algorithms by hand. Delegate to .NET primitives
  (`System.Security.Cryptography`, ASP.NET Data Protection) or established packages (Argon2id via
  Konscious, TOTP via Otp.NET, BouncyCastle when a primitive is missing).
- Never edit `frontend/src/shared/api/generated/`. Orval deletes and recreates it.

## Compose

From the repository root, `docker compose up --build` uses the development override. For a production run
from a clone, use `docker compose -f docker-compose.yml ...` so the override is not merged.

## Configuration facts

- The API reads flat environment variables. It does not load the root `.env`; Docker Compose does.
- Database variables are `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER` and
  `POSTGRES_PASSWORD`.
- `SMTP_HOST` and `SMTP_FROM_ADDRESS` are always required at startup: every login is completed with a
  one-time code (mandatory two-factor authentication), emailed by default. Locally point them at a mail
  catcher such as Mailpit; the Compose development override already does.
- New accounts always confirm their email before the first login; there is no configuration switch.
- An empty database requires valid `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD` values.
- Vite reads `frontend/.env.local`; point `VITE_API_PROXY_TARGET` to the local API.
- Production nginx is published on host port `8080` on all interfaces. Do not describe it as loopback-only.
- Configuration names in checked-in examples are uppercase. .NET nested overrides use `SECTION__KEY` and
  must also be forwarded explicitly by Compose.

## API changes

Follow the five-step contract change flow in
[ARCHITECTURE.md](ARCHITECTURE.md#changing-the-api-contract): change the backend, refresh
`frontend/swagger.json`, run `npm run api:generate`, add Spanish `errors.*` messages, then run `dotnet test`
and `npm run check`. Do not leave the committed Swagger document or generated client out of sync.
