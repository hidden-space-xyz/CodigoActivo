# CLAUDE.md — backend

ASP.NET Core Web API (.NET 10). See the repo root `CLAUDE.md` for the overall picture and the Docker stack, and `frontend/CLAUDE.md` for the SPA.

## Hard rules

- **Never use `DateTime.Now`/`DateTime.UtcNow`** — inject `IClock` (`UtcNow` as `DateTimeOffset`, `Today` as `DateOnly`, `TimeZone`).
- **Config is flat env vars** read directly (`configuration["POSTGRES_HOST"]`, `["SMTP_HOST"]`, `["DEMO_MODE"]`, …). No `dotnet user-secrets`, no `ConnectionStrings` section, no `Section:Key` binding for these. `appsettings.json` holds only app-internal knobs: `Serilog`, `Auth`, `FileStorage`, `AccountVerification`, `PasswordReset`, `ManualEmail`, `EmailGuard`, `EmailQueue`.
- **Never put `Version=` on a `PackageReference`** — versions are central in `Directory.Packages.props`.
- **Authorization is deny-by-default**: `Program.cs` uses `AddAuthorizationBuilder().SetFallbackPolicy(…)` with a `RequireAuthenticatedUser()` policy, so **every new public endpoint MUST carry `[AllowAnonymous]`** or it answers 401 to anonymous callers.
- **Adding a failure mode** = a new `ErrorCode` member + `return Error.<Kind>(ErrorCode.X)` + a Spanish message under the `errors.<ErrorCode>` key in `frontend/src/shared/i18n/locales/es.ts`.
- **Adding user-facing prose** = a `<data>` entry in `Application/Resources/Localization/AppStrings.resx` + a matching `AppStrings` member, **never a string literal**. See Localization below.
- The DB is **snake_case** (`UseSnakeCaseNamingConvention`): `FirstName` → `first_name`. Account for this in raw SQL.

## Commands

Run from `backend/`:

```bash
dotnet build CodigoActivo.slnx                 # name the .slnx: docker-compose.dcproj makes a bare `dotnet build` ambiguous (MSB1011)
dotnet build CodigoActivo.slnx -p:AlwaysReportAnalyzerWarnings=false   # skip the forced recompile when you only want a fast build
dotnet run --project src/CodigoActivo.API      # http://localhost:5150 (add --launch-profile https for :7039)

dotnet test CodigoActivo.slnx                  # unit + integration (integration auto-starts a throwaway Postgres; needs Docker)
dotnet test --filter "FullyQualifiedName~AuthControllerTests"    # one class
dotnet test --filter "DisplayName~RegisterAsync_NewAdult"        # one test by name

# EF tool once: dotnet tool install -g dotnet-ef. Migrations apply automatically on next startup:
dotnet ef migrations add <Name> --project src/CodigoActivo.Infrastructure --startup-project src/CodigoActivo.API
```

Formatting is CSharpier (defaults, no config; `.csharpierignore` excludes `**/Migrations/`).

**Local DB**: the connection string is built in code from `POSTGRES_HOST/PORT/DB/USER/PASSWORD` (defaults `localhost:5432`, db/user `codigoactivo`, empty password). Set them as environment variables, or start just the database with `docker compose up db` and set `POSTGRES_PASSWORD`. A bare `dotnet run` does **not** read the root `.env` (that is Docker-Compose-only).

**Integration tests provision their own PostgreSQL**: `PostgresContainerFixture` (an xUnit v3 assembly fixture) starts one throwaway `postgres:17-alpine` via Testcontainers, applies the real migrations once, and destroys it after the last test — no `POSTGRES_*` env vars and no pre-created database, just a running Docker daemon (a Docker-less machine fails fast with instructions). Every integration class shares that database; each test `TRUNCATE`s all tables and reseeds. **Escape hatch**: set `CODIGOACTIVO_TEST_DB_CONNECTION` to an Npgsql connection string for an empty, disposable database (a CI service container, say) to reuse it instead of spawning one.

## Configuration

Runtime knobs are flat env vars (template in the root `.env.example`), read via `IConfiguration` in `Composition/DependencyInjection.cs` and `Program.cs`:

- `POSTGRES_*` — the connection string (built with `NpgsqlConnectionStringBuilder`).
- `APP_BASE_URL`; `APP_TIMEZONE` (IANA or Windows id, auto-converted; unset → `TimeZoneInfo.Local`; the prod image sets `TZ=Europe/Madrid`); `AUTH_SAMESITE` (session + CSRF cookies).
- `DEMO_MODE` (default `false`); `ACCOUNT_VERIFICATION_REQUIRED` (unset → `true`; shipped configs set `false`) — when required, `SMTP_HOST` + `SMTP_FROM_ADDRESS` must be set or startup throws.
- `SMTP_*` for MailKit — used by all four email flows (verification, password reset, activity-signup notifications, admin-written mail).
- `FILE_STORAGE_ROOT` (default `files`, relative to the content root) — where `LocalFileSystemRepository` writes uploads; Compose points it at `/app/files`, backed by a named volume.
- `LOG_TO_FILE` (default `true`) — set `false` for stdout-only logging, skipping Serilog's daily rolling file under `<content root>/logs`. The integration test factory sets it so test hosts never write into the source tree.

**Some limits are `appsettings` colon keys, not env vars, and three of them are coupled to code elsewhere:**

- `FileStorage:MaxSizeBytes` is the **single source** of the upload limit: `[FileUploadSizeLimit]` (`API/Attributes`) applies it (+64 KiB multipart overhead) as the per-request body/form limit on the upload endpoints, and `FileService` enforces it as the business rule (`FileUploadTooLarge`). **It is also the transport limit on the three `EmailsController` actions**, which store nothing and whose own business cap is `ManualEmail:MaxAttachmentsBytes` — so lowering it below that silently caps email attachments too (and fails them as a transport error, not `EmailAttachmentsTooLarge`). Raising it also requires raising the frontend's 10 MiB thumbnail pre-check (`MAX_UPLOAD_SIZE_BYTES` in `entities/file/ui/ThumbnailField.vue`, plus its "10 MB" copy in `es.ts`) and, past 12 MiB, nginx's `client_max_body_size` in `frontend/docker/default.conf`.
- `ManualEmail:MaxRecipients` (500), `MaxAttachments` (10), `MaxAttachmentsBytes` (8 MiB). The last two are **duplicated as a client-side pre-check** in `frontend/src/features/send-email/model/useSendEmail.ts` — the server is authoritative, but raising either without editing that file leaves the extra headroom unreachable from the UI.
- `EmailGuard:*` — the outbound guard has **no on/off switch by design**; only its numbers are configurable, and every one falls back to its shipped default on a missing, zero, negative or unparseable value, so config cannot neuter it either. These keys are also *not* reachable via the `EmailGuard__X` env convention on the Docker stack: the `api` service declares an explicit environment allowlist with no `env_file:`.
- `EmailQueue:*` — `Capacity` (1000, deliberately `EmailGuard:GlobalBurst` so the guard, not the channel, is the real ceiling), `Workers` (4), `ShutdownDrainSeconds` (20) and `SendTimeoutSeconds` (60). Same fallback posture and the **same allowlist caveat** as `EmailGuard` — `EmailQueue__Capacity` does not reach the container. Two extra rules `EmailGuard` does not need, both because these values feed `CancellationTokenSource`/thread counts rather than arithmetic: **the two timespans are clamped at the top** (`MaxShutdownDrain` 5 min, `MaxSendTimeout` 10 min) and `Workers` at `MaxWorkers` (16). Without the clamp an out-of-range value is neither missing, zero, negative nor unparseable, so it would sail past `ReadTimeSpan` and then throw `ArgumentOutOfRangeException` from `CancellationTokenSource` — silently discarding **every** message inside `DeliverAsync`'s general catch, logged as if the relay were broken. `ShutdownDrainSeconds` is also coupled to `stop_grace_period: 30s` on the compose `api` service: raise it above that and Docker `SIGKILL`s mid-drain.

Options are plain objects built from config and registered as singletons — **not `IOptions<T>`**.

## Project structure (dependency rules)

```
Domain          entities, repository interfaces, Result/Error — depends on nothing
Application     services (business logic), DTOs, mapping — depends on Domain only
Infrastructure  EF Core (Npgsql), repositories, Argon2id, file storage, MailKit — depends on Domain only
Composition     all domain/application/infrastructure DI (AddCodigoActivo) — references all three
API             controllers, middleware, auth — references Composition ONLY
```

Web-host-specific wiring stays in `API/Program.cs` because it depends on ASP.NET: `AddControllers` + JSON/model-state, `AddAntiforgery`, `AddAuthentication`/`AddCookie`, `AddAuthorizationBuilder`, `AddOutputCache` policies, `ICacheInvalidator`, `AddExceptionHandler`/`AddProblemDetails`, `AddSwaggerGen`. Everything else belongs in `AddCodigoActivo`.

These rules are enforced by the `ProjectReference` graph and developer discipline only — **there is no architecture test**, so a bad reference still compiles.

## Analyzers

Analyzers (Meziantou + Sonar + the SDK's CA/IDE rules) run in-build and report as **warnings, never errors** — `dotnet build` succeeds with violations present, by design.

**Warnings are re-reported on every build, including an up-to-date one.** MSBuild normally skips `CoreCompile` when nothing changed, so the analyzers never run and a warm build prints `0 Warning(s)` with hundreds of live violations. `Directory.Build.targets` defeats that with a sentinel output file that is never created. Every build therefore pays for a full compile + analysis pass; opt out for one invocation with `-p:AlwaysReportAnalyzerWarnings=false`.

`src/Directory.Build.props` and `tests/Directory.Build.props` both `<Import>` `../Directory.Build.Analyzers.props` explicitly and both reference Meziantou.Analyzer + SonarAnalyzer. The import has to be explicit because MSBuild's implicit `Directory.Build.props` lookup stops at the **first** file it finds walking up, which is one of those two — a plain `backend/Directory.Build.props` would never be read. The test projects were unanalysed by both packs until this was wired up (`PrivateAssets=all` blocks them across the `ProjectReference` edge), so ~660 MA/S rules had never seen the ~90 test files.

### Shared with BackupZCrypt — must stay byte-identical

| File | What it holds |
|---|---|
| `Directory.Build.Analyzers.props` | every analyzer MSBuild property + the `SonarLint.xml` wiring |
| `Directory.Build.targets` | the sentinel that makes warnings print on **every** build |
| `SonarLint.xml` | thresholds for Sonar's parameterized rules — **the only place they can be set** |
| `.editorconfig` **lines 3–176** | severities, style preferences, naming rules |

That range is the single `[*.cs]` section, positioned identically in both repos so it can be compared mechanically:

```bash
cd D:/WorkSpace && B=BackupZCrypt && C=CodigoActivo/backend
for f in Directory.Build.Analyzers.props Directory.Build.targets SonarLint.xml; do diff "$C/$f" "$B/$f"; done
diff <(sed -n '3,176p' "$C/.editorconfig") <(sed -n '3,176p' "$B/.editorconfig")
```

**None of these files carries comments** — every rationale lives here in Markdown instead. Repo-local deviations go in the sections **after** line 176 and must never be added inside the shared range.

Four mechanics are easy to get wrong and are all load-bearing: `EnforceCodeStyleInBuild=true` alone enforces almost nothing on .NET 10 (the `AnalysisLevel` suffix does not reach IDE rules — `dotnet_analyzer_diagnostic.category-Style.severity = warning` is what does it); `IDE0005` needs `GenerateDocumentationFile=true`; SonarAnalyzer's rule *categories contain spaces*, so `dotnet_analyzer_diagnostic.category-…` lines for `S####` rules are discarded silently and its 139 disabled-by-default rules must be enumerated per ID; and writing `option = value:none` or `:silent` is an **unliftable** kill switch that no later `dotnet_diagnostic` entry can raise.

### Rules turned off, and why

Repo-local `[*.cs]` section (after line 176):

| Rule | Why |
|---|---|
| `CS1591` | Missing XML doc. This codebase carries no comments at all, so it produced 3088 of 4914 warnings. Left at `warning` in BackupZCrypt. `GenerateDocumentationFile` stays **on** regardless — turning it off would silently kill `IDE0005` too. |
| `MA0174` | **It and `MA0175` cannot both be satisfied.** They are exact opposites — MA0174 fires on `record Foo`, MA0175 on `record class Foo` — and `MeziantouAnalysisMode=all-warnings` enables both, so *no* declaration is clean and one of them has to be a config decision, not a code fix. MA0174 is the one disabled, keeping the plain `record` spelling used by all ~129 DTO/response records. **BackupZCrypt carries the same impossible pair and needs the same one-line fix.** |
| `MA0048` | "File name must match type name" — 151 hits, all of them the deliberate type colocation (see House style). |
| `IDE0055` | Formatting, 443 hits. CSharpier owns it, and the two disagree. |
| `MA0176` | "Optimize guid creation" — 57 hits: 17 are the fixed seed GUID literals in `DomainConstants.cs`, the other 40 the test data that must match them exactly. |
| `CA1716` | Two hits: the types `Error` and `Event`. `Error` is half the `Result` contract and `Event` is the core domain entity; renaming either over a VB keyword clash is a non-starter. |
| `MA0008` | "Add StructLayoutAttribute" — 6 hits, all `readonly record struct` helpers (`ActivitySchedule`, `RoleCapacityItem`, `EventSchedule`, `Bucket`, `EmailSendDecision`). None crosses an interop boundary. |

Per-file scopes: `S4581` in `Migrations/` (generated), `S101` in `Argon2idPasswordHasher.cs`, `S2139` in `Program.cs`, `S2068` in `DemoDataSeeder.cs` (the demo password literal). The `[tests/**/*.cs]` section drops rules that categorically do not fit test code — `CA1707` (underscore test names), `S2068` (fixture credentials), `CA1816` (xUnit's `DisposeAsync` hook), and `S1192`/`S4144`/`CA1861` (table-driven duplication). Everything else is expected to be fixed, not silenced.

### Warnings deliberately left standing

The build is *not* warning-free and is not meant to be. `dotnet build CodigoActivo.slnx -t:Rebuild` reports **175 warnings, 0 errors**, down from 1826. Everything mechanically fixable has been fixed; what remains is a reviewed list where the analyzer's suggested fix costs more than the warning. **Do not "fix" these without accepting the consequence in the same pass**, and do not silence them either — they stay visible on purpose.

Group 1 — scope the user explicitly deferred:

| Rules | Count | Why not fixed |
|---|---|---|
| `MA0191` | 23 | The `= null!` initializers on EF **navigation** properties. The required *scalar* columns were converted to `required` instead (which is why the test fixtures now set `Title`/`Description`/…), but navigations cannot be `required` — nobody assigns them, EF fixes them up — and making them nullable just pushes null-forgiving and CS8602 into `Projections.cs` and the services. |
| `CA1054` `CA1055` `CA1056` | 33 | Would change `Url`/`BaseUrl` from `string` to `Uri` on `Resource`, `ResourceDtos`, `ListQueries` and `ApplicationOptions` — rewriting the OpenAPI schema (so `frontend/swagger.json` + `npm run api:generate`) and the EF column mapping. The 14 in `Emails/` are the `siteUrl`/`url`/`accountUrl` template parameters; they reach the templates already built by `BuildSiteUrl()`/`BuildUrl()` as strings, and `ActivityEmailDetails.EventUrl` is a record member the tests construct from literals. |
| `S1200` `S107` | 15 | Class coupling and constructor parameter counts of the application services, inherent to constructor-injecting one repository per aggregate. "Fixing" them means splitting the services. |
| `CA2227` | 14 | Removing the setters from the entity navigation collections breaks the object-initializer construction used by `DemoDataSeeder` and the whole test suite. |
| `CA1034` | 7 | The nested types are `SeedIds.UserTypes`, `SeedIds.ResourceTypes`, … — the documented way to reference seeded catalogs. Un-nesting renames a contract used across all five projects. |
| `CA1308` | 7 | Email and free-text normalization must fold to **lower**case: `UserRepository`, `TextSearch`, `EmailSendLimiter` and the stored `email` column all agree on it. `ToUpperInvariant` would not match data already in the database. |
| `S104` `S3776` `S1541` | 7 | `DemoDataSeeder` is 2400 lines and complex **by design** (seven hardcoded content arrays plus one graph builder); the two big unit test files mirror it. |
| `MA0104` | 5 | `Activity` and `ResourceType` collide with BCL type names. Renaming domain entities ripples through the DB schema, the DTOs and the frontend. |
| `CA1008` | 1 | Adding a zero member to `Gender` changes an enum that serializes by name into the API contract. |

Group 2 — the analyzer's fix is technically wrong or impossible here:

| Rules | Count | Why |
|---|---|---|
| `MA0136` | 13 | Ten are the email raw-string templates: the newlines *are* the rendered message, and `.ReplaceLineEndings()` does not silence the rule anyway (it flags the literal, not its use). Three are `FormattableString` SQL passed to `Database.SqlQuery` — calling any string method on those collapses the `{fileId}`/`{pattern}` holes into the SQL text, turning parameterised queries into string concatenation. |
| `MA0181` `S1067` | 11 | The casts and predicates live inside **EF expression trees**. `(Guid?)`/`(double?)`/`(int?)` are what make `FirstOrDefault`/`AVG`/`SUM` return null for an empty set, patterns do not compile in an expression tree at all, and a per-element `Any()` predicate cannot be split into a named local without changing the semantics. |
| `MA0191` | 10 | `OpenApiFiltersTests` calls `Apply(operation, null!)`; Swashbuckle declares `OperationFilterContext` non-nullable, so dropping the `!` yields CS8625. The real fix is constructing a context, whose ctor signature is not documented in the package. |
| `IDE0046` | 5 | The two arms have no common type (`Error` vs the success value), so the ternary needs an explicit cast on *each* arm — which produced 200-character lines that then tripped `S103` **and** `S3358`. The if/else is the better code. |
| `MA0109` | 5 | A `Span<T>` overload cannot help: the members are `byte[]` because MimeKit's `BodyBuilder.Attachments.Add`/`LinkedResources.Add` need `byte[]`, and a `Span<T>` parameter is illegal on the `async Task` theory methods. |
| `CA2225` | 3 | The analyzer's naming convention for the alternate to `Result<T>`'s implicit operators resolves to the meaningless `FromT`. |
| `S1192` | 3 | The duplicated literals are Spanish demo prose used as activity `Location` values, not identifiers. |
| one-offs | 13 | `MA0040`+`S8949` (`HttpContextExtensions` cannot reach a `CancellationToken`), `MA0045` ×2 (async `Argon2id.GetBytes` would force `IPasswordHasher` async everywhere; `EmailBranding` reads the logo in a **static** initializer, where async is not available), `MA0089` ×2 (email templates), `MA0107` (`AllowOnlySelfAttribute`), `MA0149` + `xUnit1045` + `S3878` (test theory data), `CA1724` (`DependencyInjection` vs the BCL namespace), `CA1819` ×2 (`EmailMessage`'s attachment and inline-image arrays — MimeKit's shape). |

## The Result/Error pattern (core contract)

- Services return `Task<Result<TResponse>>` (or `Task<Result>` for body-less mutations). `Domain/Common/Result.cs` has implicit conversions: success is `return dto;`, failure is `return Error.NotFound(ErrorCode.UserNotFound);`.
- `ErrorCode` (`Domain/Common/ErrorCode.cs`) is one enum serialized **as a string** — the stable contract the frontend switches on.
- Controllers derive from `ApiControllerBase` and translate with `ToOk`/`ToCreated`/`ToNoContent`; `API/Extensions/ApiErrorResponseExtensions.cs` maps `ErrorKind` → HTTP status (400/401/403/404/409) and emits `ApiErrorResponse(Title, Status, Code, TraceId)`. Middleware failures (auth, CSRF, model validation, unhandled exceptions) emit the same shape.

## Localization

All backend user-facing prose lives in **`Application/Resources/Localization/AppStrings.resx`** (57 keys) and is read through the hand-written strongly-typed accessor **`AppStrings.cs`** beside it. Today that is the six email templates plus two filename fallbacks — every Spanish sentence the backend can send.

- **Every non-code asset lives under `Application/Resources/<Type>/`** — `Images/` for the email logo, `Localization/` for the resx and the `AppStrings.cs` accessor beside it (the hand-written stand-in for the `.Designer.cs` the SDK does not generate). **The folder path is not cosmetic**: it becomes the manifest resource name, which is why `AppStrings.BaseName` and the `AppStrings` namespace both read `…Application.Resources.Localization`. The resx is embedded by the SDK's implicit `**/*.resx` glob — only the *image* needs an explicit `EmbeddedResource` + `LogicalName` in the `.csproj`. Move the resx and you must edit `BaseName` in the same pass; the compiler will not notice, but every `AppStringsTests` case fails immediately.
- **A resx lives in the project that renders the string, never in Domain.** Domain carries no user-facing text by design: `Error` is `record Error(ErrorKind, ErrorCode)` with no message field precisely so copy stays at the presentation edge. If Infrastructure ever needs one it gets its own; it must not reach into Application's.
- **Keys mirror `es.ts` paths**, dotted and camelCase after the first segment: `emails.verification.subject`, `emails.activityDecision.confirmedIntro`, `files.fallbackAttachmentName`. Accessor members are the key with dots stripped and each segment PascalCased (`emails.shared.greeting` → `EmailsSharedGreeting`) — mechanical, so the guard test can enforce 1:1.
- **Composites are exposed only as methods with named, typed parameters**; `Get` is private and `string.Format` never appears at a call site. Changing a call site's argument list is then a compile error — but **the resx side is not**, because the compiler never reads it: editing a `{0}` hole there is a runtime `FormatException` (or, when a hole is *deleted*, a silently truncated string). `AppStringsTests` is what closes that gap; it invokes every accessor and asserts each value uses exactly one hole per parameter. Values use .NET's numbered `{0}` holes, **not** the `{name}` holes `es.ts` uses — the parameter name carries the meaning instead.
- **`...Text` / `...Html` suffixes** appear only where the two bodies genuinely differ. An `...Html` value may contain **only** `<b>`, `</b>` and `<br>`; every other value must contain no `<` at all, and **no value may contain a bare `&`** — most of them are interpolated into the HTML body unencoded, so an ampersand starts an entity reference. (`EmailLayout` does encode the `emails.footer.*` and `emails.shared.brandName`/`logoAlt` values, but keep the rule blanket: which hole a value lands in is not visible from the resx.) HTML scaffolding and inline CSS stay in the `.cs` templates — see The shared email layout — because encoding happens at the interpolation hole (`WebUtility.HtmlEncode`) and resource text is otherwise emitted raw.
- **The four `emails.activityDecision.*` intro/phrase keys must be translated as a unit** — the intros' trailing clause (`…y la ha aprobado`) is gender-agreed with the feminine *la inscripción* that `SignupPhrase` produces. Editing one key in isolation yields grammatical garbage, and that is invisible from the resx alone.
- `AppStrings.Get` **throws** on a missing or empty key. `ResourceManager.GetString` returns `null` silently, which would ship an empty paragraph; failing loud is the point. Its own exception message stays English (it is a developer assertion).
- The `.resx` is **comment-free** like every other file here. The four standard `<resheader>` elements are kept — they are markup, not comments, and they stop Visual Studio "repairing" the file and re-inserting its XML preamble.
- `CodigoActivo.Application.csproj` sets `<NeutralLanguage>es</NeutralLanguage>`. That emits `[assembly: NeutralResourcesLanguage("es")]`, which silences CA1824 under `AnalysisMode=All` and tells the runtime the embedded neutral set *is* Spanish. There is no `.Designer.cs`: SDK builds never generate one, and the MSBuild strongly-typed generator emits a 26-line comment header and properties only (no parameterised methods), so it is not used.

### The ceiling: a second language does not work in the container as shipped

**Verified empirically, in `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`.** The base image sets `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` in its own `ENV` (the Dockerfile's line 5 is redundant) and ships no ICU. Under that mode `CultureInfo.CurrentUICulture` **is** `CultureInfo.InvariantCulture`, `GetCultures()` returns 1, and `new CultureInfo("en")` **throws** `CultureNotFoundException` — so does `Assembly.Load("…resources, Culture=en")`. An `AppStrings.en.resx` would build an `en/CodigoActivo.Application.resources.dll` that is unreachable dead weight.

The **neutral** resources embedded in the main assembly always resolve, which is why the Spanish copy works today (the unit tests pass with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` set).

Three rules follow, and they are not optional:

- `AppStrings` must **never** read `CultureInfo.CurrentUICulture`, construct a `CultureInfo` from input, or assign `CurrentUICulture`. All three throw in production. **Never add `RequestLocalizationMiddleware`** — that is exactly what it does.
- Every lookup funnels through the one private `Culture` seam, so switching later is a one-line change.
- The neutral resx **is** the Spanish one. A second language is `AppStrings.en.resx`, never `AppStrings.es.resx`.

**Mitigation, when a second language is actually wanted** (not done in this pass):

- Cheap and verified: add `DOTNET_SYSTEM_GLOBALIZATION_PREDEFINED_CULTURES_ONLY=false` to the Dockerfile `ENV`. Satellites then load on Alpine with **zero ICU**. Two traps — the parent-culture chain is dead, so `en-US`/`en-GB` silently fall through to the neutral Spanish resource (the resolver **must normalize to the exact satellite name**), and comparison/casing/date/number formatting stay invariant, so this buys resource lookup only. It would add that variable to `DEPLOYMENT.md`.
- Proper: `apk add --no-cache icu-libs icu-data-full` **plus** `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`. **Never set that to false without installing ICU** — the process FailFasts at startup from inside `ResourceManager`'s static init. This also reverses the invariant-ordering posture that report-name sorting currently relies on, so it is a deliberate change with a re-verification cost.

### Deliberately not localized

- **Seed catalog `Name`/`Description` in `DatabaseSeeder.cs` (30 literals).** They persist as **database rows**, and `AddMissingAsync` only ever inserts — it never updates — so a resource lookup at seed time would freeze one language into the row permanently. They are also sorted and free-text-filtered **in SQL** (`SortMap` over `name`, `TextSearch.Contains`), denormalized into DTOs, the CSV export, the printable roster and outbound mail, and **admins edit them through the API**. The seeded text is a default, not a constant. Treat catalog text as **content, not UI strings**; the coherent multilingual answer is a data-model change (per-locale column or translation table), with every sort/filter moving to the resolved-locale column. `DemoDataSeeder.cs` is the same category.
- **Exception messages** (`Result.cs`, `EmailRateLimitedException`, `ApiControllerBase`, `ApiErrorResponseExtensions`) — developer assertions signalling programming errors. None is mapped to an `ApiErrorResponse`; `GlobalExceptionHandler` replaces them with the fixed 500 body, so none reaches a user. English on purpose, and greppable in stack traces.
- **Serilog message templates** — the template *is* the event's identity in the log store and aggregation keys off its hash. Localizing one fragments every dashboard built on it.
- **HTTP reason phrases** (`"Bad Request"`, `"Not Found"`, …) — RFC 9110 protocol text. They are serialized into `ApiErrorResponse.Title`, but `getErrorMessage()` short-circuits on `error.code` and never reads it, so nothing renders them. The user-visible copy already lives in `errors.*` of `es.ts`.
- **`dd/MM/yyyy` / `HH:mm`** — format patterns, not prose, rendered with `CultureInfo.InvariantCulture` deliberately. In a translator-editable file someone would write `MMMM`, which under invariant globalization renders month names **in English**.
- **robots.txt directives, sitemap XML names, `StaticPaths`, cache tags, sort keys, claim/cookie/header names** — machine-consumed grammar.
- **The brand name "Código Activo"** stays inline wherever it is spliced mid-sentence — that is the classic fragment trap, and splitting those values would produce ungrammatical translations. It *is* a key (`emails.shared.brandName`) only where it stands alone as chrome: the email header wordmark and the footer brand line, which are rendered by `EmailLayout` and never concatenated into a sentence.

`TextSearch`'s accent-folding table (`á→a`, …) would need `ü`/`ç` extended for a second locale — a data change, not a resource.

## Layer conventions

- **Services** (`Application/Services/`; interfaces colocated in `Services/Abstractions/IServices.cs`): primary-constructor DI on repository interfaces — plus `IUnitOfWork`/`IClock` for mutations, `IQueryExecutor` for paged reads, `IPasswordHasher` where needed. **Never inject `DbContext`.**
- **Persisting writes**: repositories only stage `Add`/`Remove`; commit with `IUnitOfWork.SaveChangesAsync(ct)`, which resolves to the same scoped `CodigoActivoDbContext` the repositories share.
- **DTOs** (`Application/DTOs/*Dtos.cs`): records suffixed `...Request`/`...Response`, DataAnnotations + custom attributes in `Application/Validation/ValidationAttributes.cs` (`NotBlank`, `JsonString`, …). Validation failures become `ApiErrorResponse` with `ErrorCode.RequestValidationFailed`.
- **Mapping** is hand-written: `Mapping/MappingExtensions.cs` (`ToResponse()`) and `Projections.cs` (`Expression<Func<…>>` for DB-side `Select`). No AutoMapper.
- **Pagination**: list queries derive from `PageQuery` (`Application/Querying/`, `DefaultPageSize = 25`, clamped to `MaxPageSize = 100`, plus a `Sort` string), go through `IQueryExecutor.ToPagedAsync` with a `Projections` expression, and return `PagedResult<T>` (no `Result` wrapper on lists).
- **Sorting/filtering** (`Application/Querying/`) is the backend half of the frontend's `useServerTable` contract:
  - `SortMap<T>` whitelists sort keys — `.Add(key, selector)`, `.Default(...)` when `Sort` is absent or unusable, `.Tie(selector)` for a stable tiebreaker. The `sort` param is a comma-separated list where a `-` prefix means descending, and **unknown keys are silently dropped** (a key must be `Add`ed to exist).
  - `ListQueries.cs` holds one `...ListQuery : PageQuery` per aggregate (`Event`, `Activity`, `EventCategoryType`, `EventAttendee`, `Announcement`, `Resource`, `Partner`, `User`) carrying that list's typed filters; `DashboardAnalyticsQuery.cs` is separate.
  - `LocalDayRange.LowerUtc`/`UpperExclusiveUtc` convert a `DateOnly` filter to a half-open UTC range in the app timezone — always use it for day-range filters instead of comparing raw timestamps.
  - `TextSearch.Normalize`/`Contains<T>` build the accent- and case-folded free-text predicate (`á→a`, …) as an expression EF can translate.
- **Repositories**: interfaces all in `Domain/Repositories/IDbRepositories.cs`; implementations derive from `Repository<TEntity>`. `Query()`/`GetAsync()` are `AsNoTracking()`, but `FindAsync()` returns a **tracked** entity; use `QueryWithDetails(bool tracked = false)` for includes.
- **DI lifetimes** (`Composition/DependencyInjection.cs`): DbContext/repositories/services scoped; `IClock`, `IPasswordHasher`, `IEmailSender`, `IEmailDispatcher`/`ChannelEmailDispatcher`, `ILocalFileSystemRepository`, `IQueryExecutor` and all option objects singleton. `ChannelEmailDispatcher` is registered **three times on purpose** — as itself, as `IEmailDispatcher` and via `AddHostedService` — all resolving to the one instance, because the writer and the drain loop must be the same object.
- **Email is three interfaces, and which one you inject decides whether you are rate-limited and whether you block** (`Domain/Communication/IEmailSender.cs`). `IEmailSender` has one member, `SendAsync`, and its **only** implementation is `ThrottledEmailSender` — inject this and you are guarded *and* asynchronous by construction: it spends the quota synchronously (so `EmailRateLimitedException` still reaches the caller) and then hands the message to `IEmailDispatcher` instead of waiting on SMTP. `IEmailDispatcher` has one member, `TryEnqueue`, deliberately **synchronous, `Task`-free and token-free** — that is what structurally prevents a request's `CancellationToken` from reaching a send that outlives the request. `IEmailTransport` (`SendAsync` + `SendManyAsync`, N messages over **one** reused `SmtpClient`) is the raw relay; `SmtpEmailSender` implements only it, so `AddSingleton<IEmailSender, SmtpEmailSender>()` does not compile. Exactly two types may inject `IEmailTransport` — `ChannelEmailDispatcher`'s drain loop and `EmailService`, the `[AllowOnlyAdmin]` manual flow — and exactly one may inject `IEmailDispatcher`. `UnitTests/Composition/EmailSenderWiringTests.cs` fails if a third or second one appears. All are singletons, so all connection state must stay local to the call.

## Caching (two in-memory, tag-based layers)

- **HybridCache** wraps service reads (public lists/details, seeded catalogs, event category types, dashboard counts) with stampede protection; **OutputCache** caches whole HTTP responses for the public anonymous GETs only (its default policy never caches authenticated requests — do not change that). Policies/TTLs: `Application/Caching/CachePolicies.cs` + the `AddOutputCache` block in `Program.cs` (one uniform 1-minute output TTL, which bounds the one incoherence window left: a response already in flight when a write evicts can still land in the output cache afterwards).
- **The L1 must stay bounded.** List cache keys are built from the whole query object (`CacheKeys.For`), and those queries are anonymous-caller-controlled (free-text filters, arbitrary dates, unbounded page numbers), so the key space is effectively infinite. `AddCaching` therefore gives HybridCache a `SizeLimit`ed `IMemoryCache` (64 MB) plus a 1 MB `MaximumPayloadBytes`; a key flood then just thrashes the cache instead of OOM-killing the container. Never register an unbounded `IMemoryCache` over it.
- Tags (`Application/Caching/CacheTags.cs`, nine of them) are the invalidation contract: **every write that changes a cached read must call `ICacheInvalidator.InvalidateAsync(tag, …)` right after `SaveChangesAsync`** — the API-layer implementation (`API/Caching/HttpCacheInvalidator.cs`) evicts both layers. Non-obvious edges: assignment writes invalidate `activities` (public over-capacity flags), category-type update/delete also invalidates `events` (embedded names/colors), the dashboard entry is tagged with the six entity tags it aggregates (`CacheTags.DashboardSources`), and exclusive featuring invalidates the whole list surface.
- Clients never cache, with two deliberate exceptions: `CacheControlMiddleware` stamps `Cache-Control: no-store` on every `/api` response without an explicit header; `files/{id}/content` sends `private, no-cache` + ETag instead (always revalidates, cheap 304s) and `FileService.UpdateAsync` bumps `UploadedAt` so the ETag rotates on content replacement. The SEO endpoints are the other exception (`public, max-age=1h`).
- Tests: `CodigoActivoWebAppFactory` purges all tags between tests **and after `SeedAsync`** (direct DB writes bypass invalidation); unit tests inject the pass-through `FakeHybridCache` + a substituted `ICacheInvalidator`; `CachingBehaviorTests` covers hit/invalidation flows end-to-end.

## API, auth & startup

- **Auth is a session cookie + boolean admin flag** — not JWT, not roles. `SignInAsync` adds an `isAdmin` claim only for admins; guards are the custom attributes `[AllowOnlyAdmin]` and `[AllowOnlySelf]` (`API/Attributes/`; self = the `userId` route value is the caller or the caller's child). `UserType`/`UserStatusType` are domain lookups, not auth roles — but `UserType` does gate two business rules in `ActivityService`: only `Socio` (`SeedIds.UserTypes.Member`) may take the *Líder* activity role, and only `Socio`/`Patrocinador` may sign up during an event's early window. **The first user ever registered is auto-promoted to admin.** Cookie name/expiry come from the `appsettings` `Auth` section; SameSite from `AUTH_SAMESITE`.
- **Deny by default.** The fallback policy is `RequireAuthenticatedUser()`, so an action with no authorization attribute is authenticated-only. Public reads are opt-in with `[AllowAnonymous]` (23 uses across `API/Controllers`) — forgetting it on a new public GET is the classic "why is the site 401ing" bug.
- **CSRF**: antiforgery `X-CSRF-TOKEN` header (token from `GET /api/auth/csrf`), enforced on all unsafe methods by `CsrfValidationMiddleware`.
- **Event signup windows are two-tier.** `Event` carries `SignupStartsAt`/`SignupEndsAt` plus an optional `EarlySignupStartsAt`. `ActivityService.EnsureSignupOpenAsync` is the single gate for every assignment write: admins bypass it entirely, everyone else is refused with `ActivitySignupClosed` outside `[EarlySignupStartsAt ?? SignupStartsAt, SignupEndsAt]`, and inside the early window only `Socio`/`Patrocinador` pass — others get `ActivitySignupEarlyOnly`. **A dependent minor inherits their guardian's eligibility** (the gate resolves `u.Parent == null ? u.UserTypeId : u.Parent.UserTypeId`), so a socio's children enter early too. `EarlySignupStartsAt` is nullable — when unset the event behaves as a single-tier window — and `EventService.ValidateSchedule` requires it to be strictly before `SignupStartsAt` (`EventEarlySignupNotBeforeSignup`).

### The shared email layout

**No template writes its own document.** All six messages go through one shell in `Application/Emails/`, so a change to the chrome lands everywhere at once:

- **`EmailLayout.cs`** — `Render(EmailDocument, IReadOnlyList<EmailBlock>)` returns an `EmailContent(Html, Text, InlineImages)`. It owns the whole document: `<!DOCTYPE html>`, the head (viewport, `color-scheme`, the `mso` `PixelsPerInch` block), a hidden preheader, the 600 px card, the logo header, the content cell and the common footer — plus the parallel plain-text rendering of the same thing. An `EmailBlock` is just an `(Html, Text)` pair, which is what keeps the two bodies from drifting.
- **`EmailBlocks.cs`** — the content vocabulary every template composes from: `Prose`, `Note`, `Action` (button + fallback link), `Code` (the OTP panel), `Callout`, `Panel`, `Bullets`. Blocks whose `Text` is blank are dropped, which is what keeps the plain-text body free of the double blank lines the old per-template string interpolation produced.
- **`EmailStyles.cs`** — every inline style as a named `const`. Email clients need styles inline on the element, so this is the only way they can be shared; **nothing outside this file should spell out a `style="..."` value**. The `<style>` block in `EmailLayout` carries only what cannot be inlined: the mobile media query and the `prefers-color-scheme: dark` overrides, keyed off the `ca-*` classes.
- **`EmailBranding.cs`** — the palette, mirrored from the frontend's `variables.css` tokens (`--ca-orange` → `Primary`, `--ca-on-primary` → `OnPrimary`, …) with the translucent `*-soft` tokens pre-composited over white, because email has no CSS variables. It also holds the four `EmailAccent` triples and loads the logo.

Three rules the layout depends on:

- **The accent bar is the only thing that varies, and it means status**: brand orange when there is none (verification, password reset, manual), azure for a pending signup, green for confirmed, red for denied. The CTA button stays brand orange in every message — the status colour never moves to the button.
- **The logo is a CID inline image, never a remote URL.** `EmailBranding.InlineImages` is the single instance, embedded from `Resources/Images/logo-mark.png` (an `EmbeddedResource` with an explicit `LogicalName`; **the folder path is part of the resource name**, so moving the file means editing the `.csproj` *and* `EmailBranding.LogoResourceName`, and a mismatch throws at first use, not at build) and referenced as `cid:codigoactivo-logo`. `SmtpEmailSender` adds it through MimeKit's `BodyBuilder.LinkedResources`, producing `multipart/alternative` → (`text/plain`, `multipart/related` → (`text/html`, the PNG)). A hosted URL would need the site to be publicly reachable and would be blocked by image-proxying clients; `APP_BASE_URL` defaults to `https://localhost`, so it frequently would not resolve at all. The asset is a 96 px export of the brand master in `frontend/Resources/Images/codigo-activo-logo.png`; there is no generator, so replacing the master means re-exporting this copy by hand along with the frontend icons (see `frontend/CLAUDE.md`).
- **Dark mode is progressive enhancement.** The inline styles are the light palette and always win where `<style>` is stripped; the dark block only restates the site's `.ca-dark` tokens. Because the callout's left border carries the accent, `.ca-callout` must override `border-top/right/bottom-color` individually — the `border-color` shorthand `.ca-panel` uses would repaint the accent edge grey.

`UnitTests/Application/Emails/EmailLayoutContractTests.cs` asserts the shared shell, the embedded logo and the common footer across all six messages, so a new template that bypasses `EmailLayout` fails.

### The four email flows

All four render their prose from `AppStrings` (see Localization); only HTML scaffolding and inline CSS remain in the `Emails/*.cs` templates. Key prefixes: `emails.verification.*`, `emails.passwordReset.*`, `emails.activitySignup.*` + `emails.activityDecision.*`, `emails.manual.*`, with `emails.shared.*` (greeting, brand name, logo alt text, fallback-link note, sign-off), `emails.footer.*` (tagline, website link label, automatic-message note) and `emails.details.*` (the activity/event/schedule block) shared across them.

**Every flow passes a site URL** (`application.BaseUrl.TrimEnd('/')`, via each service's `BuildSiteUrl()`) because the common footer links to it — that is why `EmailService` now injects `ApplicationOptions`. The footer note is the one piece of the footer that varies: automatic mail says "no respondas a este correo", the admin-written flow uses `emails.manual.signature` instead, because members are meant to be able to reply to it.

1. **Account verification** — `[AllowAnonymous]`: `POST /api/auth/register`, `PATCH /api/auth/{userId}/verify`, `POST /api/auth/{userId}/resend-verification`; `Application/Emails/VerificationEmail.cs`, `AccountVerification` settings.
2. **Password reset** — `[AllowAnonymous]`: `POST /api/auth/forgot-password`, `PATCH /api/auth/{userId}/reset-password`; `Application/Emails/PasswordResetEmail.cs`, `PasswordReset` settings.
**All four flows are delivered off the request thread.** Every `IEmailSender.SendAsync` call now ends at `ChannelEmailDispatcher` (see The background dispatcher), so the multi-second SMTP round trip no longer sits between the user's click and their toast. Only the admin flow (#4) still talks to `IEmailTransport` directly, because its response body reports delivered/failed counts. Two consequences to keep in mind when reading the code: an OTP or reset code is persisted after the message is **queued**, not after it is delivered, and a relay failure is logged by the worker rather than by the service that composed the message. The `try`/`catch` blocks in `AuthService`/`ActivityService` are still correct and still needed — they wrap the message-building work too, which in `ActivityService` is three DB queries — but their `catch (Exception)` arms no longer see SMTP failures, so `TrySendVerificationEmailAsync`'s `OtpLastSentAt = null` compensation is effectively vestigial. It is kept because `AuthService` must not assume its `IEmailSender` is queued; the practical cost of losing it is that a member whose verification mail fails at the relay waits out the 60 s resend cooldown instead of retrying immediately.

3. **Activity-signup notifications** — the only *automatic* flow; nobody triggers it explicitly, it rides along three `ActivityService` writes. `AssignAsync` and `AssignHouseholdAsync` send "we got your request, it is pending" (`ActivitySignupEmail.cs`); `ChangeStatusAsync` sends the admin's verdict (`ActivitySignupDecisionEmail.cs`) — **only** when the status actually changes *and* the new one is `Confirmed`/`Denied`, so re-applying a status or moving a row back to `Solicitada` stays silent. Both share `ActivityEmailDetails.cs` for the activity/event/schedule block; dates are rendered `dd/MM/yyyy HH:mm` on purpose, because the prod image runs with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` and any culture-aware month name would come out in English. Two rules the code must keep: **the recipient is the account that owns the address** — a dependent minor has none, so `ResolveRecipient` falls back to the guardian and the copy names the child instead of saying "your signup"; and **delivery never fails the write** — the assignment is already committed when the send happens, so every path is wrapped in a `try`/`catch` that logs and swallows, exactly like `AuthService`'s password-reset send.
4. **Manual (admin-written) mail** — the only `[AllowOnlyAdmin]` flow: `POST /api/emails/users/{userId}`, `POST /api/emails/users` (`[FromQuery] UserListQuery`), `POST /api/emails/events/{eventId}/attendees` (`[FromQuery] EventAttendeeListQuery`) → `EmailsController` → `EmailService` → `ManualEmail.cs`. These are the repo's only `[FromForm]` actions: `multipart/form-data` carrying `subject`, `body` and repeated `attachments` parts, with the list filters still on the query string so the admin's on-screen filters map 1:1. Nothing is persisted: no `SaveChangesAsync`, no cache invalidation, no file rows.

Three details of the manual flow are easy to break:

- **The form fields are bare lowercase action parameters, not a request record, on purpose.** Swashbuckle names multipart schema properties after the ApiExplorer parameter name verbatim (`CamelCaseQueryParametersFilter` only touches `In: Query`), so a `[FromForm] SendEmailRequest` would publish `Subject`/`Body` and Orval would generate a PascalCase body type; and `[FromForm(Name = "…")]` cannot fix it from the DTO because `Application` has no ASP.NET reference. Same reason `FilesController` takes a bare `IFormFile? file`. The DataAnnotations therefore sit on the parameters, and the controller builds the Application `SendEmailRequest` itself.
- **Recipients are resolved server-side** through the shared `Querying/UserFilters.cs` predicates — the same ones `UserService.ListAsync` and `ReportService.ListEventAttendeesAsync` use, so change them there, never by copying — and materialised with `IQueryExecutor.ToListAsync`, because `PageQuery` would clamp a paged read to 100. Users without an address (dependent minors) are skipped and counted in `SendEmailResultResponse.Skipped`.
- **Attachments are buffered to `byte[]` before the loop.** Each recipient gets their own message, so a single-read stream would arrive empty for everyone but the first.

### The outbound email guard

It sits between `IEmailSender` and the dispatcher, and covers every *automatic* flow at once, so a new flow is limited without anyone remembering to add a cooldown. **It runs on the request thread, before the message is queued** — that placement is load-bearing: it is what keeps rules 2 and 4 below true now that delivery is asynchronous. `Infrastructure/Communication/ThrottledEmailSender.cs` consults `EmailSendLimiter.cs`: two tiers of token bucket (per destination address — burst/hour/day; and process-wide, with a slice reserved for verification + password reset so an activity flood cannot take account recovery down) behind one `System.Threading.Lock`, timed off `IClock`. Four rules the code must keep:

1. **The key is the normalized destination address** — lowercased, sub-addressing folded, dots folded for gmail/googlemail only. Never the user id, which `PUT /api/users/{userId}` lets a caller repoint.
2. **Quota is spent on attempt**, so a degraded relay never buys free retries.
3. **The address table is only ever grown by an allowed send**, which is what bounds its memory: check the global bucket first and return without touching the dictionary, and fall back to global-only accounting rather than denying when it saturates.
4. **A denial must not change persisted state.** It throws `EmailRateLimitedException`, which each flow catches next to its existing `catch`: register returns 201 without nulling `OtpLastSentAt`, forgot-password keeps its unconditional `Result.Success()`, the activity notifications keep the write, and only `ResendVerificationAsync` surfaces it — reusing `ErrorCode.OtpResendCooldownActive`, so no new `ErrorCode`, swagger refresh or `es.ts` key is needed. **A full queue reuses this exact path** (`EmailLimitScope.Global`) rather than inventing a failure mode: it is a capacity denial, the quota is already spent, and every one of those `catch` blocks then does the right thing — most importantly `ResendVerificationAsync` answers 409 instead of rotating an OTP nobody will receive.

Alerts are deduplicated (`EmailGuard:AlertIntervalMinutes`); do not log per denied message. **Never add a bypass** — no `Enabled` flag, no env kill switch, no "skip when X". The integration test factory neutralises it for unrelated tests by registering `EmailGuardOptions` with very large limits, not by turning it off.

### The background dispatcher

`Infrastructure/Communication/ChannelEmailDispatcher.cs` is what removed the multi-second spinner from every user action that sends mail: a bounded `Channel<EmailMessage>` plus `EmailQueue:Workers` (4) workers draining it. `SmtpEmailSender` opens a **fresh** `SmtpClient` per message (connect + STARTTLS + AUTH + DATA + QUIT), which is the cost being moved off the request.

**Why more than one worker.** Before this change, concurrency equalled request concurrency — N simultaneous signups meant N simultaneous SMTP connections. A single drain loop would have replaced that with a strictly serial pipeline, and queue latency is not free: `OtpExpiresAt`/`PasswordResetExpiresAt` start ticking at *request* time (15 min), so a long enough backlog delivers codes that are already dead. Four workers bound the relay's concurrent connections while keeping the worst realistic backlog well inside the code lifetime. Raising it past `MaxWorkers` (16) is clamped; most relays cap concurrent connections per account anyway.

Six details are load-bearing:

- **It implements `IHostedService` directly, not `BackgroundService`** — and that is not a style choice. `BackgroundService` starts `ExecuteAsync` through a task that is cancelled by the stopping token, so if the host stops before the thread pool schedules it, **the loop never runs and everything queued is dropped**. This was observed as a flaky unit test, not theorised. Here `StartAsync` launches the drain with `Task.Run(..., CancellationToken.None)` and the loop is *never* cancellable: it ends only when the channel is completed and empty. Shutdown bounds how long we **wait**, never whether the drain happens.
- **`TryWrite`, never `WriteAsync`.** With `BoundedChannelFullMode.Wait`, `TryWrite` returns `false` on a full channel instead of blocking; `WriteAsync` would put the spinner straight back under exactly the load that motivated this.
- **The readers use `CancellationToken.None`** and `StopAsync` completes the *writer*. Cancelling a reader would throw away mail instead of sending it.
- **The transport gets a per-message timeout token**, never a request token and never the stopping token, so one hung connection wedges one worker rather than the whole queue.
- **Two non-fatal catch arms per message.** One bad recipient must never kill a worker. There is deliberately **no retry**: it would make "quota is spent on attempt" a lie, and the existing behaviour was always log-and-swallow.
- **Nothing escapes `StopAsync`.** Its two catch arms cover the drain deadline *and* anything else, because a failure to flush email must never turn a clean shutdown into a fatal host error. Delivery order is consequently **not** guaranteed — tests must assert set equality, not sequence.

The queue is in-memory by design (see [SECURITY.md](../SECURITY.md)): no EF migration, and no OTP or reset URL is ever written to `db-data`. Pending mail dies with the process, which is acceptable because the write that triggered it is already committed and a member can always request a new code.

### Participation certificates

**The code says `certificate`; the Spanish UI calls it «diploma».** `EventCertificateResponse`, `GetCertificatesAsync`, `/api/me/certificates` — English domain term everywhere, because «diploma» is the Spanish word and identifiers here are English. The user-facing wording lives only in `frontend/src/shared/i18n/locales/es.ts`.

`GET /api/me/certificates` (`MeController` → `ParticipationService.GetCertificatesAsync`) returns the caller's household's diplomas. **Nothing is persisted** — see the root `CLAUDE.md`. Five details are load-bearing:

- **The grain is `(event, member)`, not the assignment row.** The DB-side filter is `AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed && Activity.Event.EventEndsAt < clock.Today && (UserId == caller || User.ParentId == caller)`; the `DistinctBy((EventId, UserId))` runs **after** `ToListAsync` because it does not translate. A member with three confirmed activities in one event gets one diploma — the sheet names no activity, so three would be byte-identical.
- **`EventEndsAt < clock.Today` is strict**, matching `SaveRatingAsync`'s `EventRatingNotFinished` gate: an event that ends *today* has not finished, and rating and diploma must agree on that.
- **The projection uses member-init into a private `CertificateRow`**, never a positional ctor — a positional record stops EF translating (same rule as `Projections.cs`).
- **`Code` is the low 32 bits of a deterministic FNV-1a hash over the two GUIDs' bytes**, formatted `CA-{year}-{8 hex}` (`hash.ToString("X16")[8..]` — written that way rather than a `(uint)` narrowing cast, which trips MA0181). It is a *filing reference*, deliberately not a verification token: nothing can validate it, so the frontend labels it `Registro` and there is no public lookup endpoint. Never make it random or clock-derived — the same diploma must reprint identically forever. Two members of one household in one event get different codes because the user GUID is folded in.
- **No caching and no `Result<T>`.** The read is per-caller and authenticated, so HybridCache would key on the user and OutputCache never caches authenticated requests. An empty list is the natural "no diplomas yet" answer, which is why the feature needs no new `ErrorCode` and no `errors.*` key.

Ordering is `EventEndsAt` desc → `EventId` → self-before-minors → `TextSearch.Normalize`d name → `UserId`. The name keys go through `TextSearch.Normalize` + `StringComparer.Ordinal` because the prod image runs invariant globalization, where culture comparers degrade to ordinal anyway.

Covered by the `Certificates_*` tests in `IntegrationTests/Controllers/MeControllerTests.cs` (confirmed-only, finished-only, **ends-today**, dedup, one-per-household-member, household isolation, 401). `Certificates_EventEndingToday_ReturnsNothing` is the one that pins the strict `<` — the "unfinished" case sits a month past the test clock, so without it a slip to `<=` stays green. There is no `ParticipationServiceTests` unit file — this service has always been covered end-to-end, which is the right call here because the filter runs in SQL.

### Startup and the rest

- **SEO**: `SeoController` serves `GET`+`HEAD /api/sitemap.xml` and `/api/robots.txt` from `Application/Services/SitemapService.cs`, `[AllowAnonymous]` + `[ApiExplorerSettings(IgnoreApi = true)]` so they stay out of `swagger.json`, output-cached under `OutputCachePolicies.Seo` and sent with `Cache-Control: public, max-age=1h`. nginx/Vite expose them at the site root.
- **`Program.cs`**: apply migrations → run `DatabaseSeeder` (idempotent lookup catalogs keyed by fixed GUIDs in `Domain/Constants/DomainConstants.cs` `SeedIds` — reference these constants, never look catalogs up by name; `SeedIds.ResourceTypes` backs the real `ResourceType` entity/repository/configuration) → sync demo mode. `DEMO_MODE=true` seeds a realistic dataset via `DemoDataSeeder` (downloads picsum.photos images, demo login `Demo1234!`); `false` removes it on the next startup.
- **Hosted services**: `ChannelEmailDispatcher` is the only one, registered inside `AddCodigoActivo` (it is generic-host, not ASP.NET, so it does not belong in `Program.cs`). It starts at `app.RunAsync()` — after migrations, seeding and demo sync — so there is no ordering hazard.
- Serilog writes console + daily rolling compact-JSON files under `logs/` (skip the file with `LOG_TO_FILE=false`). Swagger is Development-only at `/swagger`.
- Deploy: `src/CodigoActivo.API/Dockerfile` (alpine, listens on `:8080`, runs as non-root uid 1654, HEALTHCHECK hits `/api/auth/csrf`).

## Testing conventions

- xUnit **v3**, **AwesomeAssertions** (FluentAssertions fork, same `.Should()` API), NSubstitute. Unit tests mirror the src namespace tree; hand-rolled fakes in `UnitTests/TestSupport/` (`FakePasswordHasher`, `FakeQueryExecutor`, `FakeHybridCache`, `TestClock`, `FixedTimeProvider`, `RecordingEmailSender`, `RecordingEmailDispatcher`, `RepositoryStubs`).
- **`CodigoActivo.UnitTests` touches no real infrastructure** — no database, network, file system or wall clock. The project deliberately references no DB/HTTP/file packages (EF Core InMemory is gone from the repo entirely — not referenced, not even declared in `Directory.Packages.props`), so a test needing any of those goes in `CodigoActivo.IntegrationTests` instead: that is why `LocalFileSystemRepositoryTests` (real files) and `QueryExecutorTests` (real `OFFSET`/`LIMIT` translation) live there. Its classes run **in parallel**; keep them free of shared state.
- Integration tests use `WebApplicationFactory<Program>` over the one shared container. The WebAppFactory-based classes extend `IntegrationTestBase`, which resets the clock, TRUNCATEs all tables and reseeds before each test. Parallelization is disabled assembly-wide because that database is shared — see `TestParallelization.cs`. `RepositoryTests`, `DashboardRepositoryTests` and `QueryExecutorTests` talk to the same container directly with a raw `CodigoActivoDbContext` (no web host), so real foreign keys, `NOT NULL` and unique indexes are enforced — arrange helpers must reference rows that exist. `OpenApi/SwaggerDocTests.cs` asserts the served `/swagger/v1/swagger.json` shape, the contract the frontend regenerates from. Fixed users Admin/Member/Child/Pending/Blocked (password `Str0ngPass!`) come from `TestSeedData`; log in with `LoginAsAdminAsync()`/`LoginAsMemberAsync()`. Mutating requests must go through `ApiClientExtensions.SendWithCsrfAsync` (the real CSRF middleware is active). Assert sent mail via the integration `FakeEmailSender` exposed as `Factory.EmailSender` (`.Sent`, `LastOtpSentTo(email)`) — distinct from the unit `RecordingEmailSender`. **`FakeEmailSender` is registered as both `IEmailTransport` and `IEmailDispatcher`**, which makes `TryEnqueue` deliver inline on the request thread: that is the whole determinism mechanism, and it is why ~30 tests that assert `Factory.EmailSender.Sent` immediately after an HTTP call needed no polling, no barrier and no edit when delivery went asynchronous. The real `ChannelEmailDispatcher` still starts in the test host but nothing writes to it. Its own behaviour is covered by `UnitTests/Infrastructure/Communication/ChannelEmailDispatcherTests.cs`, and the wiring (one instance serving all three registrations) by `EmailSenderWiringTests`.
- The factory injects startup config with `builder.UseSetting(...)`, **not** `ConfigureAppConfiguration`: `Program` reads `AUTH_SAMESITE` and (via `AddCodigoActivo`) `SMTP_*` while the `WebApplicationBuilder` is still being assembled, before `ConfigureAppConfiguration` sources are merged. It also sets `LOG_TO_FILE=false` so test hosts never write into `src/CodigoActivo.API/logs`.
- Test method names follow `MethodUnderTest_Scenario_ExpectedBehavior`, each segment PascalCase: `RegisterAsync_NewAdult_ReturnsCreatedAndSendsOtp`, `GetByIdWithDetailsAsync_UserMissing_ReturnsNull`. `MethodUnderTest` is the exact identifier of the method exercised, including its `Async` suffix. **Controller actions carry that suffix too** (`RegisterAsync`, `VerifyAsync`, `ListAsync`, …). That is contract-neutral and must stay so: routing is 100% attribute-based, `ToCreated` takes a literal path string rather than `CreatedAtAction`/`nameof`, and `frontend/swagger.json` carries no `operationId`, so Orval's generated client derives from path+method and never from the action name.

## House style (differs from typical .NET — don't "fix" it)

- **Type colocation is intentional**: all repository interfaces in one file, all service interfaces in one file, request+response DTOs per aggregate in one `*Dtos.cs`.
- **Private fields are `camelCase` with no leading underscore** (enforced via IDE1006).
- **No comments anywhere** — not in `.cs`, not in the MSBuild/editorconfig/XML config files. Rationale belongs in Markdown.
- Dates like birth/event dates are `DateOnly`; the clock's app zone comes from `APP_TIMEZONE`.
- Entity base classes (`Domain/Entities/Abstractions/`): `IdentifiableEntity` (client-generated `Guid Id`), `AuditableEntity` (+ Created/Updated At/By), `NamedEntity`, `IFeaturable`.
