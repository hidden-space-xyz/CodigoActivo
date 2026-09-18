# Frontend guidance

`npm run check` is the complete frontend gate: generated-client verification, type-check/build, Vitest with
90% coverage thresholds, ESLint, Steiger, Stylelint, Knip and Prettier. `npm run api:generate` regenerates the
client from `swagger.json`; `npm run api:check` verifies it is in sync.

## Rules

Feature-Sliced Design layers are `app -> pages -> widgets -> features -> entities -> shared`. Imports flow
downward; same-layer slices do not import each other; cross-slice imports go through `index.ts`. Steiger
enforces these rules.

- Import generated endpoint functions only in handwritten `api/requests.ts` wrappers.
- Keep entity-scoped TanStack Query code in the entity. Put session-dependent, multi-entity and interaction
  workflows in features, not pages.
- Build query keys through each entity's `api/query-keys.ts` factory.
- The session is a module-level reactive singleton; the project does not use Pinia.
- User-facing text must use Vue I18n keys in `src/shared/i18n/locales/es.json`.
- Feature composables use camelCase filenames; entity and `shared/lib` composables use kebab-case.
- ESLint requires JSDoc on exports, public class members, component props, emits and `defineExpose` members.
  Explain purpose and non-obvious behavior, not the name or types. Tests are exempt.
- Theme values use `--ca-*` variables; map Element Plus values to them instead of adding isolated colors.
- Tests live in `tests/unit/` and `tests/integration/`, mirroring the `src/` path, as `*.spec.ts`. They never
  need the backend: MSW (`tests/support/server.ts`) fails unhandled requests, so declare responses with
  `server.use(...)`. Mount through `tests/support/render.ts`. Keep each coverage metric at or above 90%.
