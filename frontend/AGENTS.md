# Frontend guidance

These are the frontend conventions. Commands are in [CONTRIBUTING.md](../CONTRIBUTING.md#commands); the layer
design is in [ARCHITECTURE.md](../ARCHITECTURE.md#frontend). `npm run check` is the complete frontend gate.

## Rules

Feature-Sliced Design 2.1 layers are `app -> pages -> widgets -> features -> entities -> shared`. Imports flow
downward; same-layer slices do not import each other, except through an FSD `@x` cross-import
(`entities/user/@x/account.ts`); other cross-slice imports go through `index.ts`. Code used by one page lives in
that page; move it down only when a second slice needs it. Steiger enforces these rules.

- Only `api` segments import `src/shared/api/generated`; their `mapper.ts` turns DTOs into the slice's own
  `model/types.ts`, and models never use DTO types. ESLint enforces it.
- Entities expose `xKeys`, `xQueries` (`queryOptions`), paged sources and `xMutations` (`mutationOptions` whose
  `meta.invalidates` lists the keys to refresh). Pages call them with `useQuery`/`useMutation` and add another
  slice's keys with `alsoInvalidates`.
- Views are humble: no mutations, query cache, requests or API errors in `.vue` files, `shared` included
  (ESLint enforces). Put state in `model` composables and rules in pure functions, and build forms on
  `useForm` with a pure `read`; messages a form reader returns go under a `form.problems` group.
- The session is the `GET /api/auth/me` query read through `useSession`; the project does not use Pinia.
- User-facing text must use Vue I18n keys in `src/shared/i18n/locales/es.json`. Only `app` and the `.ts` files of
  `shared` use the global i18n instance and Element Plus imperative feedback; elsewhere components call
  `useI18n`, pure helpers take a `Translate` and feedback goes through `@/shared/lib/feedback`. Keys referenced
  only through `TranslationKey`-typed config need an `ignores` entry in the `no-unused-keys` rule.
- Components use PascalCase file names; every other file uses kebab-case, such as `use-events-admin.ts`.
- ESLint requires JSDoc on exports, public class members, component props, emits and `defineExpose` members.
  Explain purpose and non-obvious behavior, not the name or types. Tests (`tests/`, `*.spec.ts`, `*.test.ts`,
  `__tests__/`) are exempt.
- Theme values use `--ca-*` variables; map Element Plus values to them instead of adding isolated colors.
- Admin tables: bind each `useServerTable` column filter to the `DataTable` `search` box or to a
  `ColumnSearch`, `ColumnFilterSelect` or `ColumnFilterDate` inside it, and pass page-owned filters as
  `extraFilters`; give columns with a `#header` slot a `label`; keep each cell slot's root a single element or
  component; build row actions with `RowActions` (no per-action colours) in a
  `width="100" align="right" fixed="right"` column.
- Keep strict TypeScript, ESLint, accessibility, i18n, Stylelint, Steiger, Knip and Prettier checks green.

## Tests

- Vitest, Vue Test Utils and jsdom. Tests live in `tests/unit/` (isolated units) and `tests/integration/`
  (components or `App.vue` mounted with real plugins), one `*.spec.ts` per source file at its `src/` path.
- They never need the backend: `tests/setup.ts` runs an MSW server that fails unhandled requests, so declare
  responses with `server.use(...)` from `tests/support/server.ts`. Requests still go through the generated
  client and `httpClient`, CSRF included.
- Mount with `renderWithProviders` or `renderApp` and run composables with `mountComposable`, from
  `tests/support/render.ts`; build API payloads with `tests/support/builders/` and model values with
  `tests/support/models.ts`. Shared state (session, CSRF token, handlers, storage, fake timers, media queries)
  resets after each test.
- Coverage covers `src/**/*.{ts,vue}` except the generated client and `main.ts`, at or above 90% for
  statements, branches, functions and lines.
- `tests/unit/docker/nginx-config.spec.ts` statically pins the `docker/*.conf` files (headers, proxy routing,
  caching); update it whenever those files change. It does not replace `nginx -t`.
