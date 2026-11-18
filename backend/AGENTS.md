# Backend guidance

Commands, including migrations and coverage, are in [CONTRIBUTING.md](../CONTRIBUTING.md#commands); the design
is in [ARCHITECTURE.md](../ARCHITECTURE.md#backend). The architecture tests in
`tests/CodigoActivo.ArchitectureTests/` and the build's analyzers enforce further conventions that this file
leaves out; each failing test or diagnostic names the rule it guards.

## Rules

- Format C# with the CSharpier tool pinned in `dotnet-tools.json`; no build or CI step checks it.
- Organize every layer by feature (Domain, Application, Infrastructure, Composition and API); only generic
  primitives are shared.
- Create entities through factories (returning `Result<T>` when a rule can fail) and change them through behavior
  methods; invariants and state transitions belong to the aggregate or to a domain policy, never to a handler.
- Aggregates have a typed identifier (`readonly record struct XId(Guid Value) : IEntityId<XId>`), or a key of
  typed identifiers like `EventTermsAcceptance`; never pass a raw `Guid` across the domain. Values with rules are
  value objects. Identifiers and value objects map onto plain columns through the EF conventions or, like the
  security state of `User`, onto complex properties with `HasColumnName`.
- Closed value sets are domain enums; their catalog tables only hold what is shown to people (name,
  description and color, and whether a resource type is external).
- Aggregates reference each other by typed identifier; declare the foreign key and its cascade with
  `HasOne<T>().WithMany().HasForeignKey(...)`, never with a navigation.
- Beyond the `AddAsync` and `Remove` of `IRepository<T>`, name repository methods after their intention.
- Aggregates raise domain events for what other parts react to; effects (emails, alerts, cache eviction,
  file cleanup, sign-out) run in `IDomainEventListener<T>` or `ICommittedEventsHandler` after the commit. An
  email that must never be lost is pending state of the aggregate instead: a scheduled command sends it and
  clears the mark, and so does any removal that would delete it first.
- Put each command or query record and its handler in one use-case file. Handlers, listeners and decorators are
  registered by contract in `UseCaseRegistration`; controllers inject `ICommandHandler<,>`/`IQueryHandler<,>`
  with `[FromServices]` and contain no business logic.
- Commands and queries carry typed identifiers and validated fields (`[property: ...]` DataAnnotations checked
  by the validation decorator). HTTP requests live in `API/<Feature>/Contracts/` with standard attributes only,
  plus the catalog membership check through `CatalogIdentifiers`, and map themselves to the command. Reuse the
  binding primitives of `Application/Common/Querying` for list criteria and write mappings by hand.
- Queries read the rows of `IReadStore` and materialize them through `IQueryExecutor` (the dashboard port
  `IDashboardCountsReader` and the log port `ILogEntryReader` are the exceptions); a cacheable query implements
  `ICachedQuery`.
- Commands use repositories and leave the commit to the unit of work decorator; they save on their own only to
  react to the outcome of the save or inside an explicit transaction.
- Expected failures return `Result`/`Result<T>` with a `DomainErrorCode` or an `ApplicationErrorCode`; do not
  throw for business outcomes. A command returns `Result`, or `Result<T>` with the typed identifier it creates
  or data that exists nowhere else afterwards; the controller runs the query for the response.
- Take the signed-in user from `ICurrentUser`, never from the command or query, and check `ActingUserPolicy` in
  every use case that acts on behalf of another person; HTTP attributes are only a first gate.
- Delete users only through `AccountEraser`, and events, activities, ratings and signups only through
  `ParticipationEraser`.
- A new entity declares `HasHistory()` or `HasNoHistory()`; a historized one changes only through the change
  tracker (no `ExecuteUpdate`/`ExecuteDelete`, no raw SQL writes), its historized dependents use
  `ClientCascade` and the eraser loads them, and a new secret column is `ExcludedFromHistory()`.
  `DataHistoryModelTests` and `DataHistoryConventionTests` enforce it.
- Add personal data a user may see to `PersonalDataReader`, the copy they download from «Mi cuenta».
- `AuditAction` and `AuditEntityType` are stored as text, so only add members to them. A handler that finds its
  main entity itself, such as an account by email, names it through `IAuditSubject`. A new command is audited
  in full unless it takes one marker of `Abstractions/Auditing/AuditMarkers.cs`; a command that proves a
  credential never takes `IRoutineCommand`. `audit_log_entries` keeps user GUIDs without a foreign key on
  purpose: its entries outlive an erased account until their retention ends, so the table keeps no data history
  and blocked data extracts leave it out.
- PostgreSQL identifiers are snake_case; remember this in raw SQL.
- User-facing backend text belongs in `Application/Common/Localization/AppStrings.resx` through
  `AppStrings`. Diagnostics, log templates, exception messages and seeded content are not UI strings.
- Declare each log event in the `*Log` class of its layer (`SecurityLog`, `ApplicationLog`, `InfrastructureLog`,
  `ApiLog`), only for what an operator must act on; normal use and expected outcomes log nothing. Log events and
  the exception messages you throw never carry a user id, personal data, a value typed by the client, a
  credential, a code, a token or a third-party reply that may quote them. See
  [SECURITY.md](../SECURITY.md#logging).
- Register services and options other than handlers and listeners in the registration under
  `CodigoActivo.Composition/` that covers their feature (`Events/` also covers activities, event categories,
  ratings and terms; `Content/` covers news, partners, resources, files and reports).
- Read deployment settings through `Settings` and `AddValidatedOptions` so an unusable value stops the start
  instead of falling back. Register options whose values are fixed in code with `AddFixedOptions`, which no
  setting can reach (`FixedOptionsTests`); it is internal to Composition, so the API registers its
  `ApiRateLimitOptions` as a plain singleton.

## Tests

- Unit tests use xUnit v3, AwesomeAssertions and NSubstitute. A test method is one PascalCase name without
  underscores that joins the member under test, the scenario and the outcome, such as
  `ToOkFailureResultReturnsProblem`; those of a handler start with `HandleAsync`.
- A rule about the shape or the dependencies of production code goes in `CodigoActivo.ArchitectureTests`, read
  from the compiled assemblies (`ProductionAssemblies`, `IlReferences`) or the sources (`BackendSources`). Its
  reviewed lists name every accepted exception: extend one only after reviewing the new case.
- Integration tests share one disposable PostgreSQL 18 instance started by Testcontainers. Each test class
  works on its own database, copied from a template migrated once (`PostgresDatabaseFixture`, or
  `Factory.Database` for classes on `CodigoActivoWebAppFactory`), so classes run in parallel and the tests of a
  class run one after another.
- The test host lifts the email guard and request rate limits (exercise them with the factory's
  `WithEmailGuard`/`WithRateLimits`), delivers the email outbox inline instead of running the background worker
  (tests that queue mail indirectly or advance the clock to a scheduled retry call
  `Factory.DrainEmailOutboxAsync()`), runs no scheduled job on its own, and never downloads the disposable
  email domain list, so every domain is accepted unless a test seeds `disposable_email_domains`.
- The k6 performance suite in `tests/performance/` at the repository root sends the requests the SPA sends when
  its pages open. Give a new or renamed endpoint that such a page requests a latency budget and add the request
  to that page's scenario; after a schema change to the tables `volume.sql` fills, update that script. See
  [tests/performance/AGENTS.md](../tests/performance/AGENTS.md).
