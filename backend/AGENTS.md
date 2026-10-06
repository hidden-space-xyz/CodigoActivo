# Backend guidance

Create migrations from `backend/` with:

```bash
dotnet ef migrations add <Name> --context CodigoActivoDbContext --project src/CodigoActivo.Infrastructure --startup-project src/CodigoActivo.API
```

`--context` is required because Infrastructure also has `CodigoActivoReadDbContext`, the read side, which
never has migrations.

Integration tests use PostgreSQL 18 through Testcontainers and require Docker unless
`CODIGOACTIVO_TEST_DB_CONNECTION` points to an empty disposable database.

CI fails when the merged unit and integration coverage (lines, branches or methods) is below 90%. Migrations are
excluded through `tests/CodeCoverage.config`; the local reproduction steps are in CONTRIBUTING.md.

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
  primitives are shared.
- Entities have private setters. Create them through factories (returning `Result<T>` when a rule can fail) and
  change them through behavior methods; invariants and state transitions belong to the aggregate or to a domain
  policy, never to a handler. Child entities change only through their root.
- Every aggregate has a typed identifier (`readonly record struct XId(Guid Value) : IEntityId<XId>`); never pass
  a raw `Guid` across the domain or a command. Values with rules are value objects. Both map onto the existing
  columns through the EF conventions, so they never change the schema.
- Closed value sets are domain enums; their catalog tables only hold names and colors, and `CatalogIds` keeps
  the stable `Guid` of each member. There is no `SeedIds`.
- Aggregates reference each other by ID, without navigations; keep foreign keys and cascades with
  `HasOne<T>().WithMany().HasForeignKey(...)`. Repositories exist only for aggregate roots, with methods that
  name their intention; no `IQueryable`, predicates or generic repository.
- Aggregates raise domain events for what other parts react to; effects (emails, alerts, cache eviction,
  file cleanup, sign-out) run in `IDomainEventListener<T>` or `ICommittedEventsHandler` after the commit, and a
  new event that changes cached data needs an entry in `CacheTagsByEvent`.
- Use one command/query record and sealed handler per use-case file. Handlers, listeners and decorators are
  registered by contract in `UseCaseRegistration`; controllers inject `ICommandHandler<,>`/`IQueryHandler<,>`
  with `[FromServices]`; there is no mediator.
- Commands and queries carry typed identifiers and validated fields (`[property: ...]` DataAnnotations checked
  by the validation decorator), never HTTP request records. HTTP requests live in `API/<Feature>/Contracts/` with
  standard attributes only and map themselves to the command.
- Queries read only through `IReadStore` rows and `IQueryExecutor`, never through repositories or domain
  entities. They never mutate, commit, invalidate caches or send email; a cacheable query implements
  `ICachedQuery` and never touches `HybridCache` itself.
- Commands use repositories and leave the commit to the unit of work decorator; they save on their own only to
  react to the outcome of the save or inside an explicit transaction. They return `Result` or `Result<TId>` and
  never call query handlers; the controller runs the query for the response. A command may read `IReadStore`
  only for lookups that change nothing (email audiences, display names). Handlers never access `DbContext`
  directly.
- Take the signed-in user from `ICurrentUser`, never from the command, and check `ActingUserPolicy` in every use
  case that acts on behalf of another person; HTTP attributes are only a first gate.
- Delete users only through `AccountEraser`. A new `User` property or table referencing users must be copied
  into `LegalCopy` (and loaded in `DeletedAccountSnapshot`) or listed in `LegalCopy.ExcludedUserProperties`, and
  a new author column must be handed over to the initial administrator in `AccountErasureStore`;
  `DeletedAccountCoverageTests` only checks that the decision was recorded, not that the data is copied or
  handed over.
- Expected failures return `Result`/`Result<T>` with a `DomainErrorCode` or an `ApplicationErrorCode`. Do not
  throw for business outcomes. The wire `ErrorCode` belongs to the API; a new code needs its member there (or a
  rename in `WireErrorCodes`) and a translation in the frontend.
- Put `*Request` records in `API/<Feature>/Contracts/` and `*Response` records in
  `Application/<Feature>/Contracts/`; shared binding primitives live in `Application/Common/Querying`.
- Use handwritten mapping. Do not introduce AutoMapper without an explicit architecture change.
- Use `IClock`; never call `DateTime.Now` or `DateTime.UtcNow`.
- PostgreSQL identifiers are snake_case; remember this in raw SQL.
- User-facing backend text belongs in `Application/Common/Localization/AppStrings.resx` through
  `AppStrings`. Diagnostics, log templates and seeded content are not UI strings.
- Private fields use `camelCase` without a leading underscore.
- Handlers and listeners need no registration. Register other services and options in the feature's
  registration under `CodigoActivo.Composition/<Feature>/`, reading settings through `Settings` and
  `AddValidatedOptions` so an unusable value stops the start instead of falling back.
- Test methods use three PascalCase segments without underscores. Handler unit tests start with
  `HandleAsync`.
