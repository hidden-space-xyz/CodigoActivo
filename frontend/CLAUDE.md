# CLAUDE.md — frontend

Vue 3 + Vite SPA (TypeScript). See the repo root `CLAUDE.md` for the overall picture and `backend/CLAUDE.md` for the API.

## Hard rules

- **Never hand-edit `src/shared/api/generated/`** — Orval wipes it (`clean: true`). Regenerate with `npm run api:generate` after refreshing `swagger.json` from the backend.
- **Import across slices only through a slice's `index.ts`** public API; the sole deep-import exception is `@/shared/api/generated/…`. Steiger (`npm run lint:fsd`) enforces the FSD layer/import rules — not ESLint.
- **All UI text is hardcoded Spanish** (no i18n) — write Spanish string literals.
- `@` is the only path alias (`→ ./src`).

## Commands

Run from `frontend/` (Node ≥ 20):

```bash
npm run dev          # Vite dev server + HMR at http://localhost:5173
npm run build        # vue-tsc typecheck + production build
npm run typecheck    # vue-tsc typecheck only
npm run lint         # ESLint (lint:fix to autofix)
npm run lint:fsd     # Steiger (Feature-Sliced Design rules)
npm run format       # Prettier over src
npm run api:generate # regenerate the typed API client from ./swagger.json (Orval)
```

There is no test suite. The dev server proxies `/api` to `VITE_API_PROXY_TARGET` (`.env.local`); the code fallback is `https://localhost:5001`, so set `http://localhost:5150` for the local backend.

## Architecture: Feature-Sliced Design

Layers under `src/` (imports flow downward only; slices in the same layer must not import each other):

- `app/` — composition root: `main.ts`, `App.vue` (picks layout from `route.meta.layout`), `providers/` (PrimeVue, TanStack Query), `router/` (routes centralized in `router/routes.ts`), `layouts/` (`DefaultLayout`, `AdminLayout`, `BlankLayout`).
- `pages/` — one slice per route; admin pages under `pages/admin/`.
- `widgets/` — `content-entity-page`: a reusable admin CRUD widget (table + create/update/delete/feature mutations). Used by the **announcements** admin page; the events and resources admin pages are bespoke (`features/manage-events`, `features/manage-resources`).
- `features/` — `auth`, `account`, `register`, and admin `manage-*` slices.
- `entities/` — business entities. Segments: `api/` (`requests.ts`, `mapper.ts`, `queries.ts`, `query-keys.ts`, `mutations.ts`), `model/` (types + state), `ui/` (cards).
- `shared/` — `api/`, `config/`, `lib/`, `ui/`.

Slices are **kebab-case** and each exposes a public API via `index.ts`. `fsd/insignificant-slice` is deliberately off in `steiger.config.ts` to allow single-consumer slices.

## API client

- `npm run api:generate` runs Orval on the committed `swagger.json` → `src/shared/api/generated/`, split into `generated/endpoints/<tag>/<tag>.ts` (plain request fns like `getApiEvents`) and `generated/models/` (DTO types + the `ErrorCode` enum). Refresh `swagger.json` from the backend's Swagger endpoint whenever the API changes.
- Orval is set to `client: 'vue-query'`, **but the app ignores the generated `useXxx` hooks**. The pattern: each entity's `api/requests.ts` wraps the generated request fns, `api/mapper.ts` maps DTO → domain type, and hand-written composables in `api/queries.ts` / `api/mutations.ts` use TanStack Query with keys from `api/query-keys.ts`. Follow this for new endpoints. (Exceptions exist: `entities/catalog` and some `manage-*` features call generated endpoints directly.)
- `src/shared/api/http-client.ts` is the Orval mutator: native `fetch`, `credentials: 'include'`, same-origin relative `/api/...` URLs. It handles CSRF transparently (fetches `/api/auth/csrf` before unsafe methods, sends `X-CSRF-TOKEN`, retries once on an invalid-token error) and throws `ApiError` (`status`, `code`, `traceId`); user-facing copy comes from `error-messages.ts`. Call `resetCsrfToken()` after login/logout (session requests already do).
- Import `ApiError`, `resetCsrfToken`, `unwrapOrNull` (404 → null), `toPage`, and `FEATURED_FIRST_SORT` (`'-featured,-createdAt'`) from the `@/shared/api` barrel.
- **Listing rule: filtering, sorting, and pagination always happen server-side.** Interactive admin tables go through `useServerTable` (`@/shared/lib`, lazy PrimeVue DataTable → query params); public growing lists go through `usePagedList` (`@/shared/lib`, "Cargar más" over `useInfiniteQuery`); design-bounded sets (household, minors, one event's activities, sponsors) are fetched in a single call with `pageSize: 100` and a server `sort`. Never re-filter/re-sort/slice fetched lists in components — add the missing query param to the backend instead.

## Routing & auth

- Guards are **per-route `beforeEnter`**, not global — `requireAuth`, `requireAdmin`, `redirectIfAuthenticated` in `src/features/auth/model/guards.ts`. The `adminRoute()` helper in `routes.ts` applies `requireAdmin` + the admin layout; all admin routes are lazy-imported. The badge-print page uses `layout: 'blank'`.
- Session state is a **module-level singleton** (no Pinia): `src/entities/session/model/session.ts`. `useSession().resolve()` lazily calls `GET /api/auth/me` (deduped in-flight); `App.vue` bootstraps it on mount. Auth is a server-set cookie; only the theme is stored in `localStorage`.

## Theming & styles

- Light/dark is custom, CSS-variable driven: `:root` holds light tokens, the `.ca-dark` class on `<html>` holds dark tokens (`src/assets/styles/variables.css`, all tokens `--ca-*`). `useTheme()` (`src/shared/lib/use-theme.ts`) toggles the class and persists `localStorage['ca-theme']`; an inline script in `index.html` applies it pre-paint. Quirk: in dark mode `--ca-orange` is remapped to cyan.
- PrimeVue 4 uses a customized Aura preset (`src/app/providers/primevue.ts`, `darkModeSelector: '.ca-dark'`), re-skinned by mapping `--p-*` tokens to `--ca-*` in `src/assets/styles/primevue-overrides.css`.
- All component styles are `<style scoped>`; global styles live only in `src/assets/styles/`.

## Conventions

- TypeScript is very strict (`noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`, `verbatimModuleSyntax`, `noUnusedLocals/Parameters`) — it rejects code that would compile in a looser project; expect it to bite.
- Composable file naming: **features** use camelCase (`useLogin.ts`); **entities and `shared/lib`** use kebab-case (`use-theme.ts`, `use-thumbnail-upload.ts`).
