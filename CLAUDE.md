# Repository guidance for coding agents

`<Codigoactivo/>` is a same-origin web application: a .NET 10 API in `backend/` and a Vue 3/TypeScript SPA in
`frontend/`. Read the document that owns the area you are changing:

- [ARCHITECTURE.md](ARCHITECTURE.md): boundaries, data flow and API contract.
- [CONTRIBUTING.md](CONTRIBUTING.md): local setup, commands and coding conventions.
- [DEPLOYMENT.md](DEPLOYMENT.md): Compose, environment variables, releases and backups.
- [SECURITY.md](SECURITY.md): authentication, trust boundaries and abuse controls.

Update the owning document in the same change when behavior, architecture, configuration or security changes.
Do not copy the same explanation into every file.

## Essential commands

From `backend/`:

```bash
dotnet build CodigoActivo.slnx
dotnet test
dotnet test tests/CodigoActivo.UnitTests --filter "FullyQualifiedName~ClassName.MethodName"
dotnet run --project src/CodigoActivo.API
dotnet ef migrations add <Name> --project src/CodigoActivo.Infrastructure --startup-project src/CodigoActivo.API
```

Integration tests use PostgreSQL 18 through Testcontainers and require Docker unless
`CODIGOACTIVO_TEST_DB_CONNECTION` points to an empty disposable database.

From `frontend/`:

```bash
npm ci
npm run dev
npm run check
npm run api:generate
npm run api:check
```

`npm run check` is the complete frontend gate: generated-client verification, type-check/build, ESLint,
Steiger, Stylelint, Knip and Prettier. There is no frontend test suite.

From the repository root, `docker compose up --build` uses the development override. For a production run
from a clone, use `docker compose -f docker-compose.yml ...` so the override is not merged.

## Configuration facts

- The API reads flat environment variables. It does not load the root `.env`; Docker Compose does.
- Database variables are `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER` and
  `POSTGRES_PASSWORD`.
- Account verification defaults to enabled. Local API startup without SMTP requires
  `ACCOUNT_VERIFICATION_REQUIRED=false`.
- An empty database requires valid `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD` values.
- Vite reads `frontend/.env.local`; point `VITE_API_PROXY_TARGET` to the local API.
- Production nginx is published on host port `8080` on all interfaces. Do not describe it as loopback-only.
- Configuration names in checked-in examples are uppercase. .NET nested overrides use `SECTION__KEY` and
  must also be forwarded explicitly by Compose.

## Backend rules

Project references flow in one direction:

```text
Domain <- Application
Domain <- Infrastructure
Domain + Application + Infrastructure <- Composition <- API
```

- Domain has no project or package references. Keep configuration options out of Domain.
- Use one command/query record and sealed handler per use-case file. Controllers inject concrete handlers;
  there is no mediator.
- Queries use no-tracking projections and `IQueryExecutor`. They never mutate, commit, invalidate caches or
  send email.
- Commands use repositories and `IUnitOfWork`; invalidate caches only after commit. Handlers never access
  `DbContext` directly.
- `RemoveAsync` and `SetFeaturedAsync` execute immediately; do not mix them with staged work expected to be
  atomic.
- Expected failures return `Result`/`Result<T>` and an `ErrorCode`. Do not throw for business outcomes.
- Put wire `*Request`/`*Response` records in `Application/DTOs` and binding queries in
  `Application/Querying`.
- Use handwritten mapping. Do not introduce AutoMapper without an explicit architecture change.
- Use `IClock`; never call `DateTime.Now` or `DateTime.UtcNow`.
- Package versions belong in `backend/Directory.Packages.props`, never on individual `PackageReference`
  items.
- PostgreSQL identifiers are snake_case; remember this in raw SQL.
- User-facing backend text belongs in `Application/Resources/Localization/AppStrings.resx` through
  `AppStrings`. Diagnostics, log templates and seeded content are not UI strings.
- Private fields use `camelCase` without a leading underscore. Warnings are errors.
- Register every new handler explicitly in `CodigoActivo.Composition/DependencyInjection.cs`; wiring tests
  detect omissions.
- Test methods use three PascalCase segments without underscores. Handler unit tests start with
  `HandleAsync`.

## Frontend rules

Feature-Sliced Design layers are `app -> pages -> widgets -> features -> entities -> shared`. Imports flow
downward; same-layer slices do not import each other; cross-slice imports go through `index.ts`. Steiger
enforces these rules.

- `@` is the only path alias and maps to `src`.
- Never edit `src/shared/api/generated/`. Orval deletes and recreates it.
- Import generated endpoint functions only in handwritten `api/requests.ts` wrappers.
- Keep entity-scoped TanStack Query code in the entity. Put session-dependent, multi-entity and interaction
  workflows in features, not pages.
- Build query keys through each entity's `api/query-keys.ts` factory.
- The session is a module-level reactive singleton; the project does not use Pinia.
- User-facing text must use Vue I18n keys in `src/shared/i18n/locales/es.json`.
- Feature composables use camelCase filenames; entity and `shared/lib` composables use kebab-case.
- Theme values use `--ca-*` variables; map Element Plus values to them instead of adding isolated colors.

## API changes

Complete cross-boundary changes in one pass:

1. Change backend endpoints, DTOs and `ErrorCode` values.
2. Refresh `frontend/swagger.json` from the Development Swagger endpoint.
3. Run `npm run api:generate` in `frontend/`.
4. Add Spanish messages for new error codes under `errors.*` in `es.json`.
5. Run `dotnet test` and `npm run check`.

Do not leave the committed Swagger document or generated client out of sync.
