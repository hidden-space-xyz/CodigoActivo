# Frontend guidance

`npm run check` is the complete frontend gate.

## Rules

Feature-Sliced Design 2.1 layers are `app -> pages -> widgets -> features -> entities -> shared`. Imports flow
downward; same-layer slices do not import each other; cross-slice imports go through `index.ts`. Code used by one
page lives in that page; move it down only when a second slice needs it. Steiger enforces these rules.

- Only `api` segments import `src/shared/api/generated`; their `mapper.ts` turns DTOs into the slice's own
  `model/types.ts`. ESLint enforces it.
- Entities expose `xKeys`, `xQueries` (`queryOptions`), paged sources and `xMutations` (`mutationOptions` whose
  `meta.invalidates` lists the keys to refresh). Pages call them with `useQuery`/`useMutation` and add another
  slice's keys with `alsoInvalidates`.
- Views are humble: no mutations, query cache or API errors in `.vue` files (ESLint enforces). Put state in
  `model` composables and rules in pure functions, and build forms on `useForm` with a pure `read`.
- The session is the `GET /api/auth/me` query read through `useSession`; the project does not use Pinia.
- User-facing text must use Vue I18n keys in `src/shared/i18n/locales/es.json`. Only `shared` and `app` use the
  global i18n instance; pure helpers take a `Translate`. Keys referenced only through `TranslationKey`-typed
  config need an `ignores` entry in the `no-unused-keys` rule.
- Components use PascalCase file names; every other file uses kebab-case.
- ESLint requires JSDoc on exports, public class members, component props, emits and `defineExpose` members.
  Explain purpose and non-obvious behavior, not the name or types. Tests are exempt.
- Theme values use `--ca-*` variables; map Element Plus values to them instead of adding isolated colors.
- Tests live in `tests/unit/` and `tests/integration/`, one `*.spec.ts` per source file at its `src/` path. They
  never need the backend: MSW (`tests/support/server.ts`) fails unhandled requests, so declare responses with
  `server.use(...)`. Mount through `tests/support/render.ts`. Keep each coverage metric at or above 90%.
