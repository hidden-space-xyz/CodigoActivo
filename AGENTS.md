# Repository guidance for coding agents

`<Codigoactivo/>` is a same-origin web application: a .NET 10 API in `backend/` and a Vue 3/TypeScript SPA in
`frontend/`. Read the document that owns the area you are changing:

- [ARCHITECTURE.md](ARCHITECTURE.md): domain language, boundaries, data flow and API contract.
- [CONTRIBUTING.md](CONTRIBUTING.md): local setup, commands, migrations, test suites and pull requests.
- [DEPLOYMENT.md](DEPLOYMENT.md): Compose, environment variables, email delivery and releases.
- [SECURITY.md](SECURITY.md): authentication, trust boundaries, abuse controls, privacy, logging and retention.

Backend and frontend coding conventions live in [backend/AGENTS.md](backend/AGENTS.md) and
[frontend/AGENTS.md](frontend/AGENTS.md), those of the end-to-end suite in
[tests/e2e/AGENTS.md](tests/e2e/AGENTS.md) and those of the performance suite in
[tests/performance/AGENTS.md](tests/performance/AGENTS.md).

## Documentation

Update the owning document in the same change when behavior, architecture, configuration or security changes.
Do not copy the same explanation into every file. Keep documentation short and factual: update the fact that
changed, remove what it replaces, and add sections only for new operational facts.

## Non-negotiable rules

- Never implement cryptographic or one-time-password algorithms by hand. Delegate to .NET primitives
  (`System.Security.Cryptography`, ASP.NET Data Protection) or established packages (Argon2id via
  Konscious, TOTP via Otp.NET, BouncyCastle when a primitive is missing).
- Never make a security, abuse or retention value configurable: keep it in code, in its `*Options` class
  registered with `AddFixedOptions`.

## Compose

From the repository root, `docker compose up --build` merges the development overlay
(`docker-compose.override.yml`). For a production run from a clone, use
`docker compose -f docker-compose.yml ...` so the overlay is not merged.

## Configuration facts

- The API reads flat environment variables plus the Compose file secrets in `/run/secrets`, each file named
  after its key. It does not load the root `.env` or `secrets/`; Docker Compose does. Secrets
  (`POSTGRES_PASSWORD`, `DATA_PROTECTION_KEY`, `SMTP_PASSWORD`) belong in `secrets/`, never in `.env` or in
  the `environment` of the `api` service.
- A value belongs in `.env` only when a deployment must change it without a release (addresses, SMTP, the
  `DEMO_MODE` choice) or when it must stay out of this public repository (the association's legal identity
  and contact details in `LEGAL_*`). Names are flat and uppercase, and Compose forwards each variable
  explicitly. The association's public identity is code: `Organization` (backend) and
  `shared/config/organization.ts` (frontend).
- `SMTP_HOST` and `SMTP_FROM_ADDRESS` are always required at startup: every login is completed with a
  one-time code (mandatory two-factor authentication), emailed by default. Locally point them at a mail
  catcher such as Mailpit; the development overlay already does.
- New accounts always confirm their email before the first login; there is no configuration switch.
- An empty database requires a valid `BOOTSTRAP_ADMIN_EMAIL`. That administrator gets a discarded random
  password and sets the first one through password reset; there is no bootstrap password.
- The designation for data protection, held by the only account that may consult the data history, is stored
  state, not configuration: the initial administrator holds it until it designates another account.
- Production nginx is published on host port `8080` on all IPv4 interfaces, not only loopback; IPv6 is not
  supported.

## API changes

Follow the five-step contract change flow in
[ARCHITECTURE.md](ARCHITECTURE.md#changing-the-api-contract) and keep the committed Swagger document in sync.
