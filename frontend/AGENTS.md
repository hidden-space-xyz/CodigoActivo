# Frontend guidance

Commands are in [CONTRIBUTING.md](../CONTRIBUTING.md#commands); the layer design is in
[ARCHITECTURE.md](../ARCHITECTURE.md#frontend). `npm run check` is the complete frontend gate: its ESLint,
Steiger, Stylelint, Knip, Prettier, architecture test, type and coverage checks enforce further conventions
that this file leaves out, and each failure names the rule it guards.

## Rules

- Entities expose `xKeys`, `xQueries` (`queryOptions`), paged sources and `xMutations` (`mutationOptions` whose
  `meta.invalidates` lists the keys to refresh). Call them with `useQuery` in views and with `useMutation` in
  `model` composables (ESLint keeps mutations and cache access out of views), and add another slice's keys with
  `alsoInvalidates`.
- Views are humble: put state in `model` composables and rules in pure functions, and build forms on `useForm`
  with a pure `read`; messages a form reader returns go under a `form.problems` group of the i18n keys.
- Read the session through `useSession`, which wraps the `GET /api/auth/me` query.
- Prefer VueUse (`@vueuse/core`, `@vueuse/router`) for timers, debounces, media queries and query-string
  state over hand-written listeners, `setTimeout`/`setInterval` handles and `router.replace` syncs.
- User-facing text must use Vue I18n keys in `src/shared/i18n/locales/es.json`. Keys referenced only through
  `TranslationKey`-typed config need an `ignores` entry in the ESLint `no-unused-keys` rule.
- Theme values use `--ca-*` variables; map Element Plus values to them instead of adding isolated colors.
- Build labeled fields with `FormField` (`shared/ui/form-field`), which renders the global `ca-field*`
  classes and links the field's message: bind its `control` with `v-bind` when the control forwards ARIA
  attributes to its input, otherwise bind only its `id` and `class`; pass the message's test id as
  `error-testid`.
- Use `ca-form-alert` and `ca-form-grid` from `app/styles/main.css` instead of scoped copies.
- `shared/config` is the one home of the association's public identity (`ORGANIZATION`), the interface
  `LOCALE` and the API limits the SPA repeats (`api-policy.ts`); never write those values as literals. The
  legal identity and contact details come from the API (`legalEntityQueries`), never from code.
- Admin tables: bind each `useServerTable` column filter to the `DataTable` `search` box or to a
  `ColumnSearch`, `ColumnFilterSelect` or `ColumnFilterDate` inside the `DataTable`, and pass page-owned
  filters as `extraFilters`; build row actions with `RowActions` (no per-action colors), the inline edit and
  delete with `editAction` and `deleteAction`.
- Elements the end-to-end suite drives carry a literal kebab-case `data-testid` named after their component
  (`login-submit`), the same on every item of a list. Test ids with a key are `page-<route>` (set on `main` by
  the default and admin layouts, and by the page itself under the blank layout), `nav-<route>` on navigation
  links, `option-<value>` on `el-option`, `row-action-<key>` from `RowActions`, and `users-scope-<value>`,
  `history-group-<key>` and `minor-dialog-<mode>` in their pages. On an Element Plus date picker the test id
  goes on the field around it, since the picker hands it to its teleported panel. Rename a test id only
  together with [tests/e2e](../tests/e2e/AGENTS.md).

## Tests

- Vitest, Vue Test Utils and jsdom. Tests live in `tests/unit/` (isolated units) and `tests/integration/`
  (components or `App.vue` mounted with real plugins), one `*.spec.ts` per source file.
- `tests/architecture/` holds the rules about the shape of `src/` that no linter checks. Its specs read the
  sources through `tests/architecture/codebase.ts` (TypeScript and Vue compiler ASTs) and run in the `node`
  environment without `tests/setup.ts`. The reviewed `@x` cross-import list is their only list of accepted
  exceptions: extend it only after reviewing the new case.
- Unit and integration tests never need the backend: `tests/setup.ts` runs an MSW server that fails unhandled
  requests, so declare responses with `server.use(...)` from `tests/support/server.ts`, or with the handler sets
  that several specs share in `tests/support/api/`. Requests still go through the generated client and
  `httpClient`, CSRF included.
- Mount with `renderWithProviders` or `renderApp` and run composables with `mountComposable`, from
  `tests/support/render.ts`; build API payloads with `tests/support/builders/` and model values with
  `tests/support/models.ts`. Each render gets its own query client, so the session starts empty, and the
  mocked handlers, the CSRF token, storage, fake timers and media queries reset after each test.
- `tests/unit/docker/nginx-config.spec.ts` statically pins the `docker/*.conf` files; it does not replace
  `nginx -t`.
