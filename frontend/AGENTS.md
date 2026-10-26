# Frontend guidance

These are the frontend conventions. Commands are in [CONTRIBUTING.md](../CONTRIBUTING.md#commands); the layer
design is in [ARCHITECTURE.md](../ARCHITECTURE.md#frontend). `npm run check` is the complete frontend gate: its
ESLint, Steiger, Stylelint, Knip, architecture test, type and coverage checks enforce further conventions that
this file leaves out, and each failure names the rule it guards.

## Rules

- Entities expose `xKeys`, `xQueries` (`queryOptions`), paged sources and `xMutations` (`mutationOptions` whose
  `meta.invalidates` lists the keys to refresh). Pages call them with `useQuery`/`useMutation` and add another
  slice's keys with `alsoInvalidates`.
- Views are humble: put state in `model` composables and rules in pure functions, and build forms on `useForm`
  with a pure `read`; messages a form reader returns go under a `form.problems` group.
- The session is the `GET /api/auth/me` query read through `useSession`.
- User-facing text must use Vue I18n keys in `src/shared/i18n/locales/es.json`. Keys referenced only through
  `TranslationKey`-typed config need an `ignores` entry in the `no-unused-keys` rule.
- Theme values use `--ca-*` variables; map Element Plus values to them instead of adding isolated colors.
- Form fields use the global `ca-field*`, `ca-form-alert` and `ca-form-grid` classes from
  `app/styles/main.css` instead of scoped copies. Link each field error to its control with
  `aria-describedby` and `aria-invalid` when the control forwards them.
- `shared/config` is the one home of the association's public identity (`ORGANIZATION`), the interface
  `LOCALE` and the API limits the SPA repeats (`api-policy.ts`); never write those values as literals. The
  legal identity and contact details come from the API (`legalEntityQueries`), never from code.
- Admin tables: bind each `useServerTable` column filter to the `DataTable` `search` box or to a
  `ColumnSearch`, `ColumnFilterSelect` or `ColumnFilterDate` inside it, and pass page-owned filters as
  `extraFilters`; build row actions with `RowActions` (no per-action colours).

## Tests

- Vitest, Vue Test Utils and jsdom. Tests live in `tests/unit/` (isolated units) and `tests/integration/`
  (components or `App.vue` mounted with real plugins), one `*.spec.ts` per source file.
- `tests/architecture/` holds the rules about the shape of `src/` that no linter checks. Its specs read the
  sources through `tests/architecture/codebase.ts` (TypeScript and Vue compiler ASTs) and run in the `node`
  environment without `tests/setup.ts`. Its reviewed lists name every accepted exception.
- Unit and integration tests never need the backend: `tests/setup.ts` runs an MSW server that fails unhandled
  requests, so declare responses with `server.use(...)` from `tests/support/server.ts`. Requests still go
  through the generated client and `httpClient`, CSRF included.
- Mount with `renderWithProviders` or `renderApp` and run composables with `mountComposable`, from
  `tests/support/render.ts`; build API payloads with `tests/support/builders/` and model values with
  `tests/support/models.ts`. Shared state (session, CSRF token, handlers, storage, fake timers, media queries)
  resets after each test.
- `tests/unit/docker/nginx-config.spec.ts` statically pins the `docker/*.conf` files; it does not replace
  `nginx -t`.
