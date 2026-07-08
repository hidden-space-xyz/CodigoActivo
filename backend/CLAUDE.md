# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

This file covers the **backend** (ASP.NET Core Web API, .NET 10). See the repository root `CLAUDE.md` for the overall picture and `frontend/CLAUDE.md` for the SPA.

## Commands

Run from `backend/`:

```bash
dotnet build                                   # analyzers run in-build; style violations fail the build
dotnet run --project src/CodigoActivo.API     # http://localhost:5150 (add --launch-profile https for :7039)

dotnet test                                    # all tests (see integration-test prerequisite below)
dotnet test --filter "FullyQualifiedName~AuthControllerTests"       # one test class
dotnet test --filter "DisplayName~Register_new_adult"               # one test by name

# Add a migration (applied automatically on next startup):
dotnet ef migrations add <Name> --project src/CodigoActivo.Infrastructure --startup-project src/CodigoActivo.API

# One-time local setup — credentials live in user secrets, never in appsettings.json:
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=codigoactivo;Username=postgres;Password=..." --project src/CodigoActivo.API
```

Formatting is CSharpier (defaults, no config file; `.csharpierignore` excludes `**/Migrations/`). Package versions are managed centrally in `Directory.Packages.props` — never put `Version=` on a `PackageReference` in a `.csproj`.

**Integration tests need a real PostgreSQL server**: `CodigoActivoWebAppFactory` uses `ConnectionStrings:Default` from user secrets with `"test"` appended to the database name (e.g. `codigoactivotest`) and throws if it's missing. No Docker/Testcontainers.

## Project structure (dependency rules are strict)

```
Domain          entities, repository interfaces, Result/Error — depends on nothing
Application     services (business logic), DTOs, mapping — depends on Domain only
Infrastructure  EF Core (Npgsql, snake_case), repositories, Argon2id, file storage, MailKit — depends on Domain only
Composition     the ONLY place DI is wired (DependencyInjection.AddCodigoActivo) — sees all three
API             controllers, middleware, auth — references Composition ONLY
```

`src/Directory.Build.props` enables `EnforceCodeStyleInBuild` + Meziantou.Analyzer + SonarAnalyzer on all src projects; `tests/Directory.Build.props` deliberately relaxes analysis and supplies the test packages (xUnit v3, AwesomeAssertions, NSubstitute, coverlet).

## The Result/Error pattern (core error-handling contract)

- Services return `Task<Result<TResponse>>` (or `Task<Result>` for body-less mutations). `Domain/Common/Result.cs` has implicit conversions, so success is `return dto;` and failure is `return Error.NotFound(ErrorCode.UserNotFound);`.
- `ErrorCode` (`Domain/Common/ErrorCode.cs`) is one big enum serialized **as a string** — it is the stable error contract the frontend switches on. To add a failure mode: add an `ErrorCode` member, return `Error.<Kind>(ErrorCode.X)`, and add the Spanish message in the frontend's `error-messages.ts`.
- Controllers derive from `ApiControllerBase` and translate with `ToOk` / `ToCreated` / `ToNoContent`; `API/Extensions/ApiErrorResponseExtensions.cs` maps `ErrorKind` → HTTP status (BadRequest 400, Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409) and emits `ApiErrorResponse(Title, Status, Code, TraceId)`. Middleware failures (auth, CSRF, unhandled exceptions, model validation) all emit the same shape.

## Layer conventions

- **Services** (`Application/Services/`): one per aggregate, primary-constructor DI, depend on repository interfaces + `IUnitOfWork` + `IClock` — never on `DbContext`. Interfaces are all colocated in `Services/Abstractions/IServices.cs`.
- **DTOs** (`Application/DTOs/*Dtos.cs`): records suffixed `...Request` / `...Response`, one file per aggregate, validated with DataAnnotations plus custom attributes in `Application/Validation/ValidationAttributes.cs` (`NotBlank`, `JsonString`, ...). Validation failures are converted globally into `ApiErrorResponse` with `ErrorCode.RequestValidationFailed`.
- **Mapping**: hand-written — `Application/Mapping/MappingExtensions.cs` (`ToResponse()` for materialized entities) and `Projections.cs` (`Expression<Func<...>>` for DB-side `Select` in paged lists). No AutoMapper.
- **Pagination**: list queries derive from `PageQuery` (`Application/Querying/`, page size clamped to 100) and go through `IQueryExecutor.ToPagedAsync` with a `Projections` expression, returning `PagedResult<T>` (no Result wrapper on lists).
- **Repositories**: interfaces in `Domain/Repositories/IDbRepositories.cs` (all in one file), implementations derive from `Repository<TEntity>` (`Infrastructure/Database/Repositories/Abstractions/`), reads are `AsNoTracking()` by default; use the `QueryWithDetails(bool tracked = false)` pattern for includes.
- **DI lifetimes** (`Composition/DependencyInjection.cs`): DbContext/repositories/services scoped; `IClock`, `IPasswordHasher`, `IEmailSender`, `ILocalFileSystemRepository`, `IQueryExecutor` and all option objects are singletons. Options are plain objects built from `IConfiguration` (not `IOptions<T>`).

## API specifics

- **Auth is a session cookie + boolean admin flag** — not JWT, not ASP.NET roles. Login calls `SignInAsync` with an `isAdmin` claim only for admins; guards are the custom attributes `[AllowOnlyAdmin]` and `[AllowOnlySelf]` (`API/Attributes/`; self = the `userId` route value is the caller or the caller's child). `UserType`/`UserStatusType` are domain lookups, not auth roles. The first user ever registered is auto-promoted to admin.
- **CSRF**: antiforgery via `X-CSRF-TOKEN` header (token from `GET /api/auth/csrf`), enforced for all unsafe methods by `CsrfValidationMiddleware`.
- Startup (`Program.cs`): applies migrations, runs `DatabaseSeeder` (idempotent lookup catalogs using fixed GUIDs from `Domain/Constants/DomainConstants.cs` `SeedIds` — services reference these constants, never query lookups by name), then syncs **demo mode**: config key `DemoMode` (currently `true` in `appsettings.json`) seeds a full realistic dataset via `DemoDataSeeder` (downloads images from picsum.photos, admin password `Demo1234!`); flipping it to `false` removes all demo data on next startup.
- Serilog writes console + rolling JSON files under `logs/`. Swagger is Development-only at `/swagger` — the frontend's committed `swagger.json` is regenerated from it.

## Testing conventions

- xUnit **v3**, **AwesomeAssertions** (FluentAssertions fork — same `.Should()` API), NSubstitute for mocks; hand-rolled fakes in `UnitTests/TestSupport/` (`FakePasswordHasher`, `TestClock`, `RecordingEmailSender`). Unit tests mirror the src namespace tree.
- Integration tests use `WebApplicationFactory<Program>` against real Postgres; every test class extends `IntegrationTestBase`, which truncates all tables and reseeds before each test (parallelization is disabled assembly-wide — tests share one DB). Fixed test users (Admin/Member/Child/Pending/Blocked, password `Str0ngPass!`) come from `TestSeedData`; log in with `LoginAsAdminAsync()`/`LoginAsMemberAsync()`. Mutating requests must go through `ApiClientExtensions.SendWithCsrfAsync` — the real CSRF middleware is active.
- Test method names are snake_case sentences: `Register_new_adult_returns_201_sends_the_otp_by_email_and_persists_pending`.

## House style (differs from typical .NET — don't "fix" it)

- **Type colocation is intentional**: all repository interfaces in one file, all service interfaces in one file, request+response DTOs per aggregate in one `*Dtos.cs`. Analyzer rules that would forbid this (MA0048 etc.) are disabled in `.editorconfig`.
- **Private fields are `camelCase` with no leading underscore** (enforced via IDE1006).
- **Never use `DateTime.Now`/`UtcNow` directly** — inject `IClock` (`UtcNow` as `DateTimeOffset`, `Today` as `DateOnly` in the app time zone, default `Europe/Madrid`). Dates like birth/event dates are `DateOnly`.
- The database is **snake_case** (`UseSnakeCaseNamingConvention()`): C# `FirstName` → column `first_name`. Remember this in raw SQL.
- Entity base classes (`Domain/Entities/Abstractions/`): `IdentifiableEntity` (client-generated `Guid Id`), `AuditableEntity` (+ CreatedAt/By, UpdatedAt/By), `NamedEntity`, `IFeaturable`.
