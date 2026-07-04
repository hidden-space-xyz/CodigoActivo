# Backend testing conventions

Two test projects under `backend/tests/`, both xUnit v3 + FluentAssertions (pinned to the free 7.x)
+ NSubstitute, coverage via coverlet. Target: **≥95% line coverage** on the production assemblies
(EF migrations excluded).

- **`CodigoActivo.UnitTests`** — fast, isolated. Collaborators are NSubstitute doubles. References
  every layer (via the API + Infrastructure projects).
- **`CodigoActivo.IntegrationTests`** — HTTP-level, through `WebApplicationFactory<Program>` on an
  EF Core **in-memory** store with the **real** cookie auth, CSRF middleware, controllers, services
  and repositories. Only the DB provider, the password hasher and the clock are swapped.

Reference files to copy patterns from (read them before writing):
- Unit service: `tests/CodigoActivo.UnitTests/Application/Services/PartnerServiceTests.cs`
- Integration controller: `tests/CodigoActivo.IntegrationTests/Controllers/PartnersControllerTests.cs`

## Hard rules

1. **FluentAssertions** for every assertion (`result.Should().Be(...)`), never raw `Assert.*`.
2. **NSubstitute** for unit doubles (`Substitute.For<IXRepository>()`, `.Returns(...)`,
   `.Received(1)`, `.DidNotReceiveWithAnyArgs()`).
3. `[Fact]` / `[Theory]` + `[InlineData]` / `[MemberData]`. Prefer `[Theory]` for tables of inputs.
4. Test **behaviour and every branch / error code**, not implementation detail. For a `Result`
   returning method assert both `IsSuccess`/`IsFailure` and the exact `Error.Kind` + `Error.Code`.
5. Tests must be **independent and deterministic** — no shared mutable statics, no wall-clock
   (`DateTime.Now`) assumptions; use the injected `TestClock`.
6. **Do not add or edit shared infrastructure** (`TestSupport/`, `Infrastructure/`). If you need a
   builder, make it a `private static` helper inside your own test file.
7. **One test file per component**, matching the source folder structure. Namespaces follow the
   folder. Do **not** build — the orchestrator builds and fixes at the end.
8. No redundant tests: each test must assert something a sibling does not.

## Unit test patterns

### Services
Constructor-inject NSubstitute doubles for every dependency. For the **read path** (`ListAsync` /
`GetByIdAsync`) inject the real `FakeQueryExecutor` (`CodigoActivo.UnitTests.TestSupport`) and stub
`repo.Query().Returns(items.AsQueryable())` — this runs the real projection, `SortMap` and
`TextSearch` in memory. Inject `TestClock` for time. Use `FakePasswordHasher` when an
`IPasswordHasher` is needed.

Cover, per method: the happy path (asserting persisted state via `repo.Received(1).AddAsync(Arg.Is<...>(...))`
and `uow.Received(1).SaveChangesAsync(...)`), **every** guard/error branch (asserting the error code
**and** that `uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default)` on failure), and read filters
(tier/search/sort/paging). Stub `repo.FindAsync(Arg.Any<Expression<Func<T,bool>>>(), Arg.Any<CancellationToken>())`.

### Pure helpers / querying / mapping / domain
Call the method directly and assert. For `Projections` and `SortMap`/`TextSearch`, build a small
in-memory `List<T>.AsQueryable()`, apply the expression, `.ToList()` and assert ordering/filtering.
`TextSearch.Contains(...)` returns an `Expression`; `.Compile()` it (or run it through `Where` on a
queryable) to exercise the fold.

### Infrastructure pure units
`Argon2idPasswordHasher`: hash→verify roundtrip true; wrong password false; malformed hash strings
(wrong segment count, bad prefix, non-numeric params, non-base64) false. `LocalFileSystemRepository`:
use a unique temp dir (`Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())`) as `RootPath`,
`IDisposable` to clean up; test save/open/delete roundtrip, `OpenReadAsync` null for missing, and
that path-traversal names (`"../x"`, `"a/b"`) throw `ArgumentException`. `SystemClock`: `Today`
reflects the supplied `TimeZoneInfo`.

### API units
- `ApiErrorResponseExtensions`: `MapKind` for each `ErrorKind` → status/title; unsupported kind (cast
  an out-of-range `(ErrorKind)99`) throws `ArgumentOutOfRangeException` (invoke via `Create`).
- `ClaimsPrincipalExtensions`: build a `ClaimsPrincipal` with a `ClaimTypes.NameIdentifier` /
  `ClaimTypes.Role` and assert `GetUserId()` / `IsAdmin()`; also the missing/unparseable id → null.
- Filters (`AllowOnlyAdminAttribute`, `AllowOnlySelfAttribute`): construct an
  `AuthorizationFilterContext` over a `DefaultHttpContext` (set `.User`, `.RequestServices` with a
  `ServiceCollection` containing any `Substitute.For<IUserRepository>()` needed, and `RouteData`
  values), call `OnAuthorization`/`OnAuthorizationAsync`, and assert `context.Result` is
  `ChallengeResult` / `ForbidResult` / null.
- `GlobalExceptionHandler`: `TryHandleAsync(new DefaultHttpContext{ Response.Body = new MemoryStream() }, new Exception(), default)`
  returns true, sets 500, writes the `UnexpectedError` body.

## Integration test patterns

Derive from `IntegrationTestBase` (`CodigoActivo.IntegrationTests.Infrastructure`); it reseeds a fresh
in-memory DB before each test. Primary-ctor form: `public sealed class XTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)`.

Clients & auth:
- `CreateClient()` — anonymous, cookie-aware.
- `await LoginAsAdminAsync()` / `await LoginAsMemberAsync()` / `await LoginAsync(TestSeedData.XCredentials)`.
- Unsafe verbs go through `client.PostJsonAsync(url, body)`, `PutJsonAsync`, `PatchJsonAsync(url, body?)`,
  `DeleteWithCsrfAsync(url)` — these fetch a fresh CSRF token automatically. For a **negative CSRF**
  test send a raw `HttpRequestMessage` with no token and expect 400 `InvalidCsrfToken`.
- Read a body: `await response.ReadJsonAsync<T>()`. Error bodies: `ReadJsonAsync<ApiErrorResponse>()`
  (`CodigoActivo.API.Extensions`), assert `.Code` (an `ErrorCode`). Lists:
  `ReadJsonAsync<PagedResult<XResponse>>()`.

Seed / verify domain data:
- `await Factory.SeedAsync(db => { db.X.Add(...); return Task.CompletedTask; })`.
- `await Factory.QueryAsync(db => db.X.FindAsync(id).AsTask())`.
- Fixed users: `TestSeedData.Users.AdminId / MemberId / MemberChildId / PendingId / BlockedId`, emails
  `TestSeedData.AdminEmail` etc., password `TestSeedData.Password`. Reference catalog GUIDs come from
  `CodigoActivo.Domain.Constants.SeedIds`.
- Entities needing a thumbnail FK: seed a `FileEntity` first (in-memory ignores FKs but the service
  checks existence) — see `SeedThumbnailAsync` in the reference file.

Cover per controller: anonymous vs admin vs member vs owner-self on each endpoint (expect
200/201/204 vs 401 vs 403), model-validation 400 (`RequestValidationFailed`), not-found 404 with the
resource's error code, pagination envelope shape, and persistence side effects verified via
`Factory.QueryAsync`.

## Running

From `backend/`:
- `dotnet test CodigoActivo.slnx` — all tests.
- Coverage: `dotnet test CodigoActivo.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings`
  then `reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:coveragereport -reporttypes:TextSummary`.
