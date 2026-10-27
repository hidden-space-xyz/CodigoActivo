# Backend guidance

These are the backend conventions. Commands, including migrations and coverage, are in
[CONTRIBUTING.md](../CONTRIBUTING.md#commands); the design is in [ARCHITECTURE.md](../ARCHITECTURE.md#backend).
The architecture tests in `tests/CodigoActivo.ArchitectureTests/` and the build's analyzers enforce further
conventions that this file leaves out; each failure names the rule it guards. Format C# with the CSharpier tool
pinned in `dotnet-tools.json`; no build or CI step checks it.

## Rules

- Organize every layer by feature (Domain, Application, Infrastructure, Composition and API); only generic
  primitives are shared.
- Create entities through factories (returning `Result<T>` when a rule can fail) and change them through behavior
  methods; invariants and state transitions belong to the aggregate or to a domain policy, never to a handler.
- Aggregates have a typed identifier (`readonly record struct XId(Guid Value) : IEntityId<XId>`), or a key of
  typed identifiers like `EventTermsAcceptance`; never pass a raw `Guid` across the domain. Values with rules are
  value objects. Both map onto plain columns through the EF conventions or, like the security state of `User`, as
  complex properties with `HasColumnName`.
- Closed value sets are domain enums; their catalog tables only hold the name, description and color shown to
  people.
- Aggregates keep foreign keys and cascades to each other with `HasOne<T>().WithMany().HasForeignKey(...)`.
  Repository methods name their intention; there is no generic repository.
- Aggregates raise domain events for what other parts react to; effects (emails, alerts, cache eviction,
  file cleanup, sign-out) run in `IDomainEventListener<T>` or `ICommittedEventsHandler` after the commit.
- Put each command or query record and its handler in one use-case file. Handlers, listeners and decorators are
  registered by contract in `UseCaseRegistration`; controllers inject `ICommandHandler<,>`/`IQueryHandler<,>`
  with `[FromServices]` and contain no business logic.
- Commands and queries carry typed identifiers and validated fields (`[property: ...]` DataAnnotations checked
  by the validation decorator). HTTP requests live in `API/<Feature>/Contracts/` with standard attributes only,
  plus the catalog membership check through `CatalogIdentifiers`, and map themselves to the command.
- Queries read through `IReadStore` rows and `IQueryExecutor` (the dashboard port `IDashboardCountsReader` and the
  log port `ILogEntryReader` are the exceptions); a cacheable query implements `ICachedQuery`.
- Commands use repositories and leave the commit to the unit of work decorator; they save on their own only to
  react to the outcome of the save or inside an explicit transaction. They return `Result`, or `Result<T>` with
  the typed ID they create or data that exists nowhere else afterwards; the controller runs the query for the
  response.
- Take the signed-in user from `ICurrentUser`, never from the command or query, and check `ActingUserPolicy` in
  every use case that acts on behalf of another person; HTTP attributes are only a first gate.
- Delete users only through `AccountEraser`, and events, activities, ratings and signups only through
  `ParticipationEraser`. A new entity declares `HasHistory()` or `HasNoHistory()`; a historized one changes only
  through the change tracker (no `ExecuteUpdate`/`ExecuteDelete`, no raw SQL writes), its historized dependents
  use `ClientCascade` and the eraser loads them, and a new secret column is `ExcludedFromHistory()`.
  `DataHistoryModelTests` and `DataHistoryConventionTests` enforce it. Personal data a user may see also goes
  into `PersonalDataReader`, the copy they download from «Mi cuenta».
- `AuditAction` and `AuditEntityType` are stored as text, so their members are only ever added. A handler that
  finds its main entity itself, such as an account by email, names it through `IAuditSubject`.
  `audit_log_entries` keeps user GUIDs without a foreign key on purpose: its entries outlive an erased account
  until their retention ends, so the table keeps no data history and is not handed over.
- Expected failures return `Result`/`Result<T>` with a `DomainErrorCode` or an `ApplicationErrorCode`. Do not
  throw for business outcomes.
- Shared binding primitives live in `Application/Common/Querying`. Mapping is handwritten.
- PostgreSQL identifiers are snake_case; remember this in raw SQL.
- User-facing backend text belongs in `Application/Common/Localization/AppStrings.resx` through
  `AppStrings`. Diagnostics, log templates, exception messages and seeded content are not UI strings.
- Declare each log event in the `*Log` class of its layer (`SecurityLog`, `ApplicationLog`, `InfrastructureLog`,
  `ApiLog`), only for what an operator must act on; normal use and expected outcomes log nothing. Log events and
  the exception messages you throw never carry a user id, personal data, a value typed by the client, a
  credential, a code, a token or a third-party reply that may quote them. See
  [SECURITY.md](../SECURITY.md#logging).
- Handlers and listeners need no registration. Register other services and options in the registration under
  `CodigoActivo.Composition/` that covers their feature (`Events/` also covers activities, event categories,
  ratings and terms; `Content/` covers news, partners, resources, files and reports). Read deployment settings
  through `Settings` and `AddValidatedOptions` so an unusable value stops the start instead of falling back;
  register options whose values are fixed in code with `AddFixedOptions`, which no setting can reach
  (`FixedOptionsTests`).

## Tests

- Unit tests use xUnit v3, AwesomeAssertions and NSubstitute. Test methods use three PascalCase segments, such as
  `ToOkFailureResultReturnsProblem`.
- A rule about the shape or the dependencies of production code goes in `CodigoActivo.ArchitectureTests`, read
  from the compiled assemblies (`ProductionAssemblies`, `IlReferences`) or the sources (`BackendSources`). Its
  reviewed lists name every accepted exception: extend one only after reviewing the new case.
- Integration tests share one disposable PostgreSQL 18 Testcontainers instance. Each test class works on its own
  database, copied from a template migrated once (`PostgresDatabaseFixture`, or `Factory.Database` for classes
  on `CodigoActivoWebAppFactory`), so classes run in parallel and the tests of a class run one after another.
  The test host lifts the email guard and request rate limits (exercise them with the factory's
  `WithEmailGuard`/`WithRateLimits`), delivers the email outbox inline instead of running the background worker
  (tests that queue mail indirectly or advance the clock to a scheduled retry call
  `Factory.DrainEmailOutboxAsync()`), and never downloads the disposable email domain list, so every domain is
  accepted unless a test seeds `disposable_email_domains`.
- The k6 performance suite in `tests/performance/` calls every endpoint. A new or renamed endpoint gets a latency
  budget in `k6/lib/catalog.js` and a request in the scenario of its audience, named `METHOD /api/route/{param}`;
  the `smoke` profile must report every catalogued endpoint as exercised.
