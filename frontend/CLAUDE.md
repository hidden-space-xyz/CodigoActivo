# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

This file covers the **frontend** (Vue 3 + Vite SPA). See the repository root `CLAUDE.md` for the overall picture and `backend/CLAUDE.md` for the API.

## Commands

Run from `frontend/`:

```bash
npm run dev          # Vite dev server with HMR at http://localhost:5173
npm run build        # vue-tsc typecheck + production build
npm run typecheck    # vue-tsc only (--force)
npm run lint         # ESLint (npm run lint:fix to autofix)
npm run lint:fsd     # Steiger — enforces Feature-Sliced Design layer rules
npm run format       # Prettier over src/**/*.{ts,vue,css}
npm run api:generate # Regenerate the typed API client from ./swagger.json (Orval)
```

There is no test suite. Node ≥ 20 required. The dev server proxies `/api` to `VITE_API_PROXY_TARGET` (set in `.env.local`, default `https://localhost:5001` — point it at `http://localhost:5150` for the local backend).

## Architecture: Feature-Sliced Design

Layers under `src/` (imports only flow downward; slices in the same layer must not import each other — enforced by **Steiger**, not ESLint):

- `app/` — composition root: `main.ts`, `App.vue` (selects layout from `route.meta.layout`), `providers/` (PrimeVue, TanStack Query), `router/` (all routes centralized in `router/routes.ts`), `layouts/` (`DefaultLayout`, `AdminLayout`, `BlankLayout`).
- `pages/` — one slice per route; admin pages nested under `pages/admin/`.
- `widgets/` — `content-entity-page`: reusable admin CRUD widget (table + create/update/delete/feature mutations) used by the admin content pages.
- `features/` — `auth`, `account`, `register`, and admin `manage-*` slices.
- `entities/` — business entities. Segments: `api/` (`requests.ts`, `mapper.ts`, `queries.ts`, `query-keys.ts`, `mutations.ts`), `model/` (types + state), `ui/` (cards).
- `shared/` — `api/` (HTTP client + generated code), `config/`, `lib/`, `ui/`.

Conventions: slices are **kebab-case**; every slice exposes a **public API via `index.ts`** and other slices import only through it (the sole deep-import exception is `@/shared/api/generated/...`). The `fsd/insignificant-slice` rule is deliberately disabled in `steiger.config.ts` to keep single-consumer slices. Only path alias: `@` → `./src`.

## API client (the part that's easy to get wrong)

- `npm run api:generate` runs Orval on the **committed** `swagger.json` and regenerates `src/shared/api/generated/` (`clean: true` — everything in there is wiped and rewritten; **never hand-edit generated files**). When the backend API changes, refresh `swagger.json` from the backend's Swagger endpoint first.
- Orval is configured with `client: 'vue-query'` so it emits `useXxx` hooks, **but the app does not use the generated hooks**. The pattern is: each entity's `api/requests.ts` wraps the generated *plain request functions* (e.g. `getApiEvents`), `api/mapper.ts` maps DTO → domain type, and hand-written composables in `api/queries.ts` / `api/mutations.ts` use TanStack Query with keys from `api/query-keys.ts`. Follow this pattern for new endpoints.
- `src/shared/api/http-client.ts` is the Orval mutator: native `fetch`, `credentials: 'include'` (cookie session), same-origin relative `/api/...` URLs. It **transparently handles CSRF**: fetches `/api/auth/csrf` before unsafe methods, sends `X-CSRF-TOKEN`, retries once on an invalid-token error. Call `resetCsrfToken()` after login/logout (the session entity's requests already do). Failures throw `ApiError` (`status`, `code`, `traceId`); user-facing copy comes from `src/shared/api/error-messages.ts`.
- Helpers in `src/shared/api/rest.ts`: `unwrapOrNull` (404 → null), `toPage`, `fetchAllPages`.

## Routing & auth

- Guards are **per-route `beforeEnter`**, not global — `requireAuth`, `requireAdmin`, `redirectIfAuthenticated` in `src/features/auth/model/guards.ts`. The `adminRoute()` helper in `routes.ts` applies `requireAdmin` + the admin layout; all admin routes are lazy-imported. The badge-print page uses `layout: 'blank'`.
- Session state is a **module-level singleton** (no Pinia): `src/entities/session/model/session.ts`. `useSession().resolve()` lazily calls `GET /api/auth/me` (deduped in-flight). `App.vue` bootstraps it on mount. Auth itself is a server-set cookie; nothing is stored in localStorage except the theme.

## Theming & styles

- Light/dark is custom, CSS-variable driven: `:root` holds light tokens, the `.ca-dark` class on `<html>` holds dark tokens (`src/assets/styles/variables.css`, all tokens `--ca-*`). `useTheme()` (`src/shared/lib/use-theme.ts`) toggles the class and persists to `localStorage['ca-theme']`; an inline script in `index.html` applies it pre-paint. Quirk: in dark mode `--ca-orange` is remapped to cyan.
- PrimeVue 4 uses a customized Aura preset (`src/app/providers/primevue.ts`, `darkModeSelector: '.ca-dark'`) and is re-skinned by mapping `--p-*` tokens to `--ca-*` in `src/assets/styles/primevue-overrides.css`.
- All component styles are `<style scoped>` — keep it that way; global styles live only in `src/assets/styles/`.

## Other conventions

- All UI text is **hardcoded Spanish** (no i18n library); write user-facing strings in Spanish.
- TypeScript is very strict: `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`, `verbatimModuleSyntax`, `noUnusedLocals/Parameters` — expect these to bite.
- Prettier: no semicolons, single quotes, width 100, trailing commas.
- Composable naming is mixed on purpose: entity/feature composables are camelCase (`useLogin.ts`); `shared/lib` uses kebab-case (`use-theme.ts`).
