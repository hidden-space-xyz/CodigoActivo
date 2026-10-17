# Backend guidance

These are the backend conventions. Commands, including migrations and coverage, are in
[CONTRIBUTING.md](../CONTRIBUTING.md#commands); the design is in [ARCHITECTURE.md](../ARCHITECTURE.md#backend).
The unit tests under `tests/CodigoActivo.UnitTests/Architecture/` enforce most of these rules.

## Rules

Project references flow in one direction:

```text
Domain <- Application
Domain + Application <- Infrastructure
Domain + Application + Infrastructure <- Composition <- API
```

- Domain has no project or package references. Keep configuration options, technical ports and serialization
  attributes out of Domain; it declares only the repositories of its aggregates.
- Organize every layer by feature (Domain, Application, Infrastructure, Composition and API); only generic
  primitives are shared. Application features never depend on each other in a cycle (Users depends on Accounts,
  not the reverse), and `Abstractions` and `Common` depend on no feature; `FeatureDependencyTests` reads the IL.
- Entities have private setters. Create them through factories (returning `Result<T>` when a rule can fail) and
  change them through behavior methods; invariants and state transitions belong to the aggregate or to a domain
  policy, never to a handler. Child entities change only through their root.
- Aggregates have a typed identifier (`readonly record struct XId(Guid Value) : IEntityId<XId>`), or a key of
  typed identifiers like `EventTermsAcceptance`; never pass a raw `Guid` across the domain or a command. Values with rules are value objects. Both map onto plain columns
  through the EF conventions or, like the security state of `User`, as complex properties with `HasColumnName`.
- Closed value sets are domain enums; their catalog tables only hold the name, description and color shown to
  people, and `CatalogIds` keeps the stable `Guid` of each member.
- Aggregates reference each other by ID, without navigations; keep foreign keys and cascades with
  `HasOne<T>().WithMany().HasForeignKey(...)`. Repositories exist only for aggregate roots, with methods that
  name their intention; no `IQueryable`, predicates or generic repository.
- Aggregates raise domain events for what other parts react to; effects (emails, alerts, cache eviction,
  file cleanup, sign-out) run in `IDomainEventListener<T>` or `ICommittedEventsHandler` after the commit, and a
  new event that changes cached data needs an entry in `CacheTagsByEvent`.
- Use one command/query record and sealed handler per use-case file. Handlers, listeners and decorators are
  registered by contract in `UseCaseRegistration`; controllers inject `ICommandHandler<,>`/`IQueryHandler<,>`
  with `[FromServices]` and contain no business logic; there is no mediator.
- Commands and queries carry typed identifiers and validated fields (`[property: ...]` DataAnnotations checked
  by the validation decorator), never HTTP request records. HTTP requests live in `API/<Feature>/Contracts/` with
  standard attributes only, plus the catalog membership check through `CatalogIdentifiers`, and map themselves to
  the command.
- Queries read only through `IReadStore` rows and `IQueryExecutor` (the dashboard port
  `IDashboardCountsReader` and the log port `ILogEntryReader` are the exceptions), never through repositories
  or domain entities. They never
  mutate, commit, invalidate caches or send email; a cacheable query implements `ICachedQuery` and never
  touches `HybridCache` itself.
- Commands use repositories and leave the commit to the unit of work decorator; they save on their own only to
  react to the outcome of the save or inside an explicit transaction. They return `Result` or `Result<TId>` and
  never call query handlers; the controller runs the query for the response. A command may read `IReadStore`
  only for lookups that change nothing (email audiences, display names). Handlers never access `DbContext`
  directly.
- Take the signed-in user from `ICurrentUser`, never from the command or query, and check `ActingUserPolicy` in
  every use case that acts on behalf of another person; HTTP attributes are only a first gate.
- Delete users only through `AccountEraser`. A new `User` property or table referencing users must be copied
  into `LegalCopy` (and loaded in `DeletedAccountSnapshot`) or listed in `LegalCopy.ExcludedUserProperties`
  (a value object member as `Property.Member`), and a new author column must be handed over to the initial
  administrator in `AccountErasureStore`; `DeletedAccountCoverageTests` only checks that the decision was
  recorded, not that the data is copied or handed over.
- Every command is audited. A new command needs its `AuditAction` member, named after it without `Command`,
  and its translation in the frontend, or `IUnaudited` when recording it would break a promise such as rating
  anonymity; `AuditConventionTests` checks both. `AuditAction` and `AuditEntityType` are stored as text, so
  their members are only ever added. A new `bool` or enum parameter of a command becomes an audit detail and
  must be added to the reviewed list in `AuditConventionTests`; never let one carry personal data. A handler
  that finds its main entity itself, such as an account by email, names it through `IAuditSubject`.
  `audit_log_entries` keeps user GUIDs without a foreign key on purpose: its entries outlive an erased account
  until their retention ends, so the table is neither copied into `LegalCopy` nor handed over.
- A new `StoredFileId` or `RichText` column must be checked by `StoredFileRepository.GetInUseAsync`, and a
  `StoredFileId` column needs a foreign key to `StoredFile` with `DeleteBehavior.Restrict`;
  `FileReferenceCoverageTests` lists them.
- Expected failures return `Result`/`Result<T>` with a `DomainErrorCode` or an `ApplicationErrorCode`. Do not
  throw for business outcomes. The wire `ErrorCode` belongs to the API; a new code needs its member there (or a
  rename in `WireErrorCodes`) and a translation in the frontend.
- Put `*Request` records in `API/<Feature>/Contracts/` and `*Response` records in
  `Application/<Feature>/Contracts/`; shared binding primitives live in `Application/Common/Querying`. Mapping
  is handwritten.
- Use `IClock`; never call `DateTime.Now` or `DateTime.UtcNow`. `ClockConventionTests` reads the IL.
- PostgreSQL identifiers are snake_case; remember this in raw SQL.
- User-facing backend text belongs in `Application/Common/Localization/AppStrings.resx` through
  `AppStrings`. Diagnostics, log templates, exception messages and seeded content are not UI strings.
- Declare every log event with `[LoggerMessage]` in its `*Log` class (`SecurityLog`, `ApplicationLog`,
  `InfrastructureLog`, `ApiLog`) at `Warning` or above, only for what an operator must act on; normal use and
  expected outcomes log nothing. `CA1848` is an error. Use only the placeholder types the
  `LoggingConventionTests`/`LoggingCallSiteTests` allow-list accepts, and never a user id, personal data, a
  value typed by the client, a credential, a code, a token or a third-party reply that may quote them. Exception
  messages you throw follow the same rule because they are logged. `NormalUseLoggingTests` fails when a normal
  flow writes an entry. See [SECURITY.md](../SECURITY.md#logging).
- Handlers and listeners need no registration. Register other services and options in the feature's
  registration under `CodigoActivo.Composition/<Feature>/`, reading settings through `Settings` and
  `AddValidatedOptions` so an unusable value stops the start instead of falling back.
- Package versions are centralized in `Directory.Packages.props`; `PackageReference` items carry no `Version`.
- Private fields use `camelCase` without a leading underscore.
- Keep CSharpier formatting and every SDK analyzer rule clean; warnings are errors. Document every public type
  and member with XML comments (`CS1591` fails the build); test projects are exempt.

## Tests

- Unit tests use xUnit v3, AwesomeAssertions and NSubstitute. Test methods use three PascalCase segments without
  underscores, such as `RegisterAsyncNewAdultReturnsCreatedAndSendsOtp`; handler unit tests start with
  `HandleAsync`.
- Integration tests share one disposable PostgreSQL 18 Testcontainers instance and run without parallelism.
  The test host lifts the email guard and request rate limits (exercise them with the factory's
  `WithEmailGuard`/`WithRateLimits`), delivers the email outbox inline instead of running the background worker
  (tests that queue mail indirectly or advance the clock to a scheduled retry call
  `Factory.DrainEmailOutboxAsync()`), and never downloads the disposable email domain list, so every domain is
  accepted unless a test seeds `disposable_email_domains`.
