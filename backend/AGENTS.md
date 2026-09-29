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
- Organize every layer by feature. Each feature keeps its `Commands/`, `Queries/` and `Contracts/` together;
  only generic primitives are shared.
- Entities have private setters. Create them through factories (returning `Result<T>` when a rule can fail) and
  change them through behavior methods; invariants and state transitions belong to the aggregate or to a domain
  policy, never to a handler. Child entities change only through their root.
- Aggregates reference each other by ID, without navigations; keep foreign keys and cascades with
  `HasOne<T>().WithMany().HasForeignKey(...)`. Repositories exist only for aggregate roots, with methods that
  name their intention; no `IQueryable`, predicates or generic repository. Fixed catalogs are reference data
  without a repository.
- Use one command/query record and sealed handler per use-case file. Controllers inject concrete handlers;
  there is no mediator.
- Queries read only through `IReadStore` rows and `IQueryExecutor`, never through repositories or domain
  entities. They never mutate, commit, invalidate caches or send email.
- Commands use repositories and `IUnitOfWork`; invalidate caches only after commit. They return `Result` or
  `Result<Guid>` and never call query handlers; the controller runs the query for the response. A command may
  read `IReadStore` only for lookups that change nothing (fixed catalogs, email audiences, display names).
  Handlers never access `DbContext` directly.
- Delete users only through `AccountEraser`. A new `User` property or table referencing users must be copied
  into `LegalCopy` (and loaded in `DeletedAccountSnapshot`) or listed in `LegalCopy.ExcludedUserProperties`, and
  a new author column must be handed over to the initial administrator in `AccountErasureStore`;
  `DeletedAccountCoverageTests` only checks that the decision was recorded, not that the data is copied or
  handed over.
- Expected failures return `Result`/`Result<T>` and an `ErrorCode`. Do not throw for business outcomes.
- Put wire `*Request`/`*Response` records in the feature's `Contracts/`; shared binding primitives live in
  `Application/Common/Querying`.
- Use handwritten mapping. Do not introduce AutoMapper without an explicit architecture change.
- Use `IClock`; never call `DateTime.Now` or `DateTime.UtcNow`.
- PostgreSQL identifiers are snake_case; remember this in raw SQL.
- User-facing backend text belongs in `Application/Common/Localization/AppStrings.resx` through
  `AppStrings`. Diagnostics, log templates and seeded content are not UI strings.
- Private fields use `camelCase` without a leading underscore.
- Register every new handler explicitly in `CodigoActivo.Composition/DependencyInjection.cs`; wiring tests
  detect omissions.
- Test methods use three PascalCase segments without underscores. Handler unit tests start with
  `HandleAsync`.
