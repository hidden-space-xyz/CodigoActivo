# CLAUDE.md — backend

ASP.NET Core Web API (.NET 10). See the repo root `CLAUDE.md` for the overall picture and the Docker deploy stack, and `frontend/CLAUDE.md` for the SPA.

## Hard rules

- **Never use `DateTime.Now`/`DateTime.UtcNow`** — inject `IClock` (`UtcNow` as `DateTimeOffset`, `Today` as `DateOnly`, `TimeZone`).
- **Config is flat env vars** read directly (`configuration["POSTGRES_HOST"]`, `["SMTP_HOST"]`, `["DEMO_MODE"]`, …). There are no `dotnet user-secrets`, no `ConnectionStrings` section, and no `Section:Key` binding for these. `appsettings.json` holds only app-internal knobs (`Serilog`, `Auth`, `FileStorage`, `AccountVerification`).
- **Never put `Version=` on a `PackageReference`** — versions are central in `Directory.Packages.props`.
- **Adding a failure mode** = add an `ErrorCode` member + `return Error.<Kind>(ErrorCode.X)` + a Spanish message in the frontend's `error-messages.ts`.
- The DB is **snake_case** (`UseSnakeCaseNamingConvention`): `FirstName` → `first_name`. Account for this in raw SQL.

## Commands

Run from `backend/`:

```bash
dotnet build                                   # analyzers run in-build; style violations fail the build
dotnet run --project src/CodigoActivo.API      # http://localhost:5150 (add --launch-profile https for :7039)

dotnet test CodigoActivo.slnx                  # unit + integration (integration auto-starts a throwaway Postgres; needs Docker)
dotnet test --filter "FullyQualifiedName~AuthControllerTests"    # one class
dotnet test --filter "DisplayName~Register_new_adult"           # one test by name

# EF tool once: dotnet tool install -g dotnet-ef. Migrations apply automatically on next startup:
dotnet ef migrations add <Name> --project src/CodigoActivo.Infrastructure --startup-project src/CodigoActivo.API
```

Formatting is CSharpier (defaults, no config; `.csharpierignore` excludes `**/Migrations/`).

**Local DB**: the connection string is built in code from `POSTGRES_HOST/PORT/DB/USER/PASSWORD` (defaults `localhost:5432`, db/user `codigoactivo`, empty password). Set them as environment variables, or start just the database with `docker compose up db` and set `POSTGRES_PASSWORD`. A bare `dotnet run` does **not** read the root `.env` file (that is Docker-Compose-only).

**Integration tests provision their own PostgreSQL**: `PostgresContainerFixture` (an xUnit v3 assembly fixture) starts one throwaway `postgres:17-alpine` container via Testcontainers, applies the real migrations once, and destroys it after the last test — no `POSTGRES_*` env vars and no pre-created database, just a running Docker daemon (a Docker-less machine fails fast with instructions). Every integration class shares that one database; each test `TRUNCATE`s all tables and reseeds. **Escape hatch**: set `CODIGOACTIVO_TEST_DB_CONNECTION` to an Npgsql connection string for an empty, disposable database (a CI service container, say) to reuse it instead of spawning one. There is no EF Core InMemory anywhere in the integration project — only the unit tests still fake the store.

## Configuration

Runtime knobs are flat env vars (template in the root `.env.example`), read via `IConfiguration` in `Composition/DependencyInjection.cs` and `Program.cs`:

- `POSTGRES_*` — the connection string (built with `NpgsqlConnectionStringBuilder`).
- `APP_BASE_URL`; `APP_TIMEZONE` (IANA or Windows id, auto-converted; unset → `TimeZoneInfo.Local`; the prod image sets `TZ=Europe/Madrid`); `AUTH_SAMESITE` (session + CSRF cookies).
- `DEMO_MODE` (default `false`); `ACCOUNT_VERIFICATION_REQUIRED` (unset → `true`; shipped configs set `false`) — when required, `SMTP_HOST` + `SMTP_FROM_ADDRESS` must be set or startup throws.
- `SMTP_*` for MailKit.

Options are plain objects built from config and registered as singletons — **not `IOptions<T>`**.

## Project structure (dependency rules)

```
Domain          entities, repository interfaces, Result/Error — depends on nothing
Application     services (business logic), DTOs, mapping — depends on Domain only
Infrastructure  EF Core (Npgsql), repositories, Argon2id, file storage, MailKit — depends on Domain only
Composition     the ONLY place DI is wired (AddCodigoActivo) — references all three
API             controllers, middleware, auth — references Composition ONLY
```

These rules are enforced by the `ProjectReference` graph and developer discipline only — **there is no architecture test**, so a bad reference still compiles; keep the graph clean by hand. `src/Directory.Build.props` enables `EnforceCodeStyleInBuild` + Meziantou.Analyzer + SonarAnalyzer for all src projects; `tests/Directory.Build.props` turns on `EnforceCodeStyleInBuild` too and supplies the shared test packages (xUnit v3, AwesomeAssertions, NSubstitute, coverlet). The handful of rules that categorically don't fit test code — `CA1707` (the snake_case test names), `S2068` (fixture credentials), `CA1816` (xUnit's `DisposeAsync` lifecycle hook) — are scoped off in the `[tests/**/*.cs]` section of `.editorconfig`, each with its reason. Everything else is expected to be fixed, not silenced.

## The Result/Error pattern (core contract)

- Services return `Task<Result<TResponse>>` (or `Task<Result>` for body-less mutations). `Domain/Common/Result.cs` has implicit conversions: success is `return dto;`, failure is `return Error.NotFound(ErrorCode.UserNotFound);`.
- `ErrorCode` (`Domain/Common/ErrorCode.cs`) is one enum serialized **as a string** — the stable contract the frontend switches on.
- Controllers derive from `ApiControllerBase` and translate with `ToOk`/`ToCreated`/`ToNoContent`; `API/Extensions/ApiErrorResponseExtensions.cs` maps `ErrorKind` → HTTP status (400/401/403/404/409) and emits `ApiErrorResponse(Title, Status, Code, TraceId)`. Middleware failures (auth, CSRF, model validation, unhandled exceptions) emit the same shape.

## Layer conventions

- **Services** (`Application/Services/`; interfaces colocated in `Services/Abstractions/IServices.cs`): primary-constructor DI on repository interfaces — plus `IUnitOfWork`/`IClock` for mutations, `IQueryExecutor` for paged reads, `IPasswordHasher` where needed. **Never inject `DbContext`.**
- **Persisting writes**: repositories only stage `Add`/`Remove`; commit with `IUnitOfWork.SaveChangesAsync(ct)`, which resolves to the same scoped `CodigoActivoDbContext` the repositories share.
- **DTOs** (`Application/DTOs/*Dtos.cs`): records suffixed `...Request`/`...Response`, DataAnnotations + custom attributes in `Application/Validation/ValidationAttributes.cs` (`NotBlank`, `JsonString`, …). Validation failures become `ApiErrorResponse` with `ErrorCode.RequestValidationFailed`.
- **Mapping** is hand-written: `Mapping/MappingExtensions.cs` (`ToResponse()`) and `Projections.cs` (`Expression<Func<…>>` for DB-side `Select`). No AutoMapper.
- **Pagination**: list queries derive from `PageQuery` (`Application/Querying/`, size clamped to 100), go through `IQueryExecutor.ToPagedAsync` with a `Projections` expression, and return `PagedResult<T>` (no `Result` wrapper on lists).
- **Repositories**: interfaces all in `Domain/Repositories/IDbRepositories.cs`; implementations derive from `Repository<TEntity>`. `Query()`/`GetAsync()`/`GetAllAsync()` are `AsNoTracking()`, but `FindAsync()` returns a **tracked** entity; use `QueryWithDetails(bool tracked = false)` for includes.
- **DI lifetimes** (`Composition/DependencyInjection.cs`): DbContext/repositories/services scoped; `IClock`, `IPasswordHasher`, `IEmailSender`, `ILocalFileSystemRepository`, `IQueryExecutor`, and all option objects singleton.

## API, auth & startup

- **Auth is a session cookie + boolean admin flag** — not JWT, not roles. `SignInAsync` adds an `isAdmin` claim only for admins; guards are the custom attributes `[AllowOnlyAdmin]` and `[AllowOnlySelf]` (`API/Attributes/`; self = the `userId` route value is the caller or the caller's child). `UserType`/`UserStatusType` are domain lookups, not auth roles. **The first user ever registered is auto-promoted to admin.** Cookie name/expiry come from the `appsettings` `Auth` section; SameSite from `AUTH_SAMESITE`.
- **CSRF**: antiforgery `X-CSRF-TOKEN` header (token from `GET /api/auth/csrf`), enforced on all unsafe methods by `CsrfValidationMiddleware`.
- **Startup** (`Program.cs`): apply migrations → run `DatabaseSeeder` (idempotent lookup catalogs keyed by fixed GUIDs in `Domain/Constants/DomainConstants.cs` `SeedIds` — reference these constants, never look catalogs up by name) → sync demo mode. `DEMO_MODE=true` seeds a realistic dataset via `DemoDataSeeder` (downloads picsum.photos images, demo login `Demo1234!`); `false` removes it on the next startup.
- Serilog writes console + daily rolling compact-JSON files under `logs/`. Swagger is Development-only at `/swagger`.
- Deploy: `src/CodigoActivo.API/Dockerfile` (alpine, listens on `:8080`, runs as non-root uid 1654, HEALTHCHECK hits `/api/auth/csrf`); the root `CLAUDE.md` documents the Compose stack.

## Testing conventions

- xUnit **v3**, **AwesomeAssertions** (FluentAssertions fork, same `.Should()` API), NSubstitute. Unit tests mirror the src namespace tree; hand-rolled fakes in `UnitTests/TestSupport/` (`FakePasswordHasher`, `TestClock`, `RecordingEmailSender`).
- Integration tests use `WebApplicationFactory<Program>` over one throwaway `postgres:17-alpine` container shared by the whole assembly; the WebAppFactory-based classes extend `IntegrationTestBase`, which resets the clock, TRUNCATEs all tables and reseeds before each test (parallelization is disabled assembly-wide — tests share one DB). `RepositoryTests` talks to that same container directly with a raw `CodigoActivoDbContext` (no web host), so real foreign keys, `NOT NULL` and unique indexes are enforced — arrange helpers must reference rows that exist. Fixed users Admin/Member/Child/Pending/Blocked (password `Str0ngPass!`) come from `TestSeedData`; log in with `LoginAsAdminAsync()`/`LoginAsMemberAsync()`. Mutating requests must go through `ApiClientExtensions.SendWithCsrfAsync` (the real CSRF middleware is active). Assert sent mail via the integration `FakeEmailSender` exposed as `Factory.EmailSender` (`.Sent`, `LastOtpSentTo(email)`) — distinct from the unit `RecordingEmailSender`.
- Test method names are snake_case sentences: `Register_new_adult_returns_201_sends_the_otp_by_email_and_persists_pending`.

## House style (differs from typical .NET — don't "fix" it)

- **Type colocation is intentional**: all repository interfaces in one file, all service interfaces in one file, request+response DTOs per aggregate in one `*Dtos.cs`. Analyzer rules that would forbid this (MA0048, …) are disabled in `.editorconfig`.
- **Private fields are `camelCase` with no leading underscore** (enforced via IDE1006).
- Dates like birth/event dates are `DateOnly`; the clock's app zone comes from `APP_TIMEZONE`.
- Entity base classes (`Domain/Entities/Abstractions/`): `IdentifiableEntity` (client-generated `Guid Id`), `AuditableEntity` (+ Created/Updated At/By), `NamedEntity`, `IFeaturable`.
