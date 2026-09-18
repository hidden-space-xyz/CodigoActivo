# Backend guidance

Create migrations from `backend/` with:

```bash
dotnet ef migrations add <Name> --project src/CodigoActivo.Infrastructure --startup-project src/CodigoActivo.API
```

Integration tests use PostgreSQL 18 through Testcontainers and require Docker unless
`CODIGOACTIVO_TEST_DB_CONNECTION` points to an empty disposable database.

CI fails when the merged unit and integration coverage (lines, branches or methods) is below 90%. Migrations are
excluded through `tests/CodeCoverage.config`; the local reproduction steps are in CONTRIBUTING.md.

## Rules

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
- PostgreSQL identifiers are snake_case; remember this in raw SQL.
- User-facing backend text belongs in `Application/Resources/Localization/AppStrings.resx` through
  `AppStrings`. Diagnostics, log templates and seeded content are not UI strings.
- Private fields use `camelCase` without a leading underscore.
- Register every new handler explicitly in `CodigoActivo.Composition/DependencyInjection.cs`; wiring tests
  detect omissions.
- Test methods use three PascalCase segments without underscores. Handler unit tests start with
  `HandleAsync`.
