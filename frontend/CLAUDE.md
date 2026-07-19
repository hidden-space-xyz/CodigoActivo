# CLAUDE.md — frontend

Vue 3 + Vite SPA (TypeScript). See the repo root `CLAUDE.md` for the overall picture and `backend/CLAUDE.md` for the API.

Stack (see `package.json` for exact ranges): Vue 3.5 + vue-router 5, TypeScript 6 / vue-tsc 3, **Vite 8 (Rolldown bundler)**, PrimeVue 4.5 + `@primeuix/themes` 2, TanStack Vue Query 5, vue-i18n 11, TipTap 3, Chart.js 4, ESLint 10 (flat config) + Prettier 3, Steiger 0.5, Orval 8. Node ≥ 20.

## Hard rules

- **Never hand-edit `src/shared/api/generated/`** — Orval wipes it (`clean: true`). Regenerate with `npm run api:generate` after refreshing `swagger.json` from the backend.
- **Import across slices only through a slice's `index.ts`** public API; the sole deep-import exception is `@/shared/api/generated/…`. Steiger (`npm run lint:fsd`) enforces the FSD layer/import rules — not ESLint.
- **All UI text goes through Vue I18n — never hardcode string literals.** Messages live centrally in `src/shared/i18n/locales/es.ts` (namespaced: `common`, `nav`, `errors`, `pages.*`, `features.*`, `entities.*`, …). Use `$t('key')` in templates, `const { t } = useI18n()` in `<script setup>`, and `i18n.global.t('key')` (from `@/shared/i18n`) outside setup (mappers, router guards, plain modules). The app is **Spanish-only** (single `es` locale); a second language is a drop-in `en.ts` next to `es.ts`. PrimeVue built-ins are localized via `src/shared/i18n/primevue-locale.ts`.
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
- `entities/` — business entities. Segments: `api/` (`requests.ts`, `mapper.ts`, `queries.ts`, `query-keys.ts`, `mutations.ts`), `model/` (types + state), `ui/` (cards). **`entities/feed/` and `entities/participation/` are empty leftover directories** (no files, no `index.ts`, untracked by git) — dead, not a pattern to copy or extend.
- `shared/` — `api/`, `config/`, `i18n/`, `lib/`, `ui/`.

Slices are **kebab-case** and each exposes a public API via `index.ts`. `fsd/insignificant-slice` is deliberately off in `steiger.config.ts` to allow single-consumer slices.

## API client

- `npm run api:generate` runs Orval on the committed `swagger.json` → `src/shared/api/generated/`, split into `generated/endpoints/<tag>/<tag>.ts` (plain request fns like `getApiEvents`) and `generated/models/` (DTO types + the `ErrorCode` enum). Refresh `swagger.json` from the backend's Swagger endpoint whenever the API changes.
- Orval is set to `client: 'vue-query'`, **but the app ignores the generated `useXxx` hooks**. The pattern: each entity's `api/requests.ts` wraps the generated request fns, `api/mapper.ts` maps DTO → domain type, and hand-written composables in `api/queries.ts` / `api/mutations.ts` use TanStack Query with keys from `api/query-keys.ts`. Follow this for new endpoints on the **public** side. The admin side largely skips the wrapper: `entities/catalog`, every `manage-*` feature and most `pages/admin/*` import generated endpoint fns directly into their composables/components — that is the established admin pattern, not a violation to clean up.
- `src/shared/api/http-client.ts` is the Orval mutator: native `fetch`, `credentials: 'include'`, same-origin relative `/api/...` URLs. It handles CSRF transparently (fetches `/api/auth/csrf` before unsafe methods, sends `X-CSRF-TOKEN`, retries once on an invalid-token error) and throws `ApiError` (`status`, `code`, `traceId`). User-facing copy comes from the `errors.*` namespace in `src/shared/i18n/locales/es.ts` — one key per backend `ErrorCode` value — resolved by `getErrorMessage(error, fallback?)` in `src/shared/lib/api-error.ts` (falls back to `errors.generic` when the code has no key). A new backend `ErrorCode` means a new `errors.<Code>` key. Call `resetCsrfToken()` after login/logout (session requests already do).
- **The frontend never HTTP-caches `/api`**: every fetch goes out with `cache: 'no-store'`, and the QueryClient uses `staleTime: 0` (always refetch on mount) — do not raise either; server-side OutputCache/HybridCache absorb the load. Images (`<img src="/api/files/{id}/content">`) revalidate on every use via ETag (`no-cache`), so a replaced file shows up immediately.
- Import `ApiError`, `resetCsrfToken`, `unwrapOrNull` (404 → null), `toPage`, and `FEATURED_FIRST_SORT` (`'-featured,-createdAt'`) from the `@/shared/api` barrel.
- **Listing rule: filtering, sorting, and pagination always happen server-side.** Interactive admin tables go through `useServerTable` (`@/shared/lib`, lazy PrimeVue DataTable → query params); public growing lists go through `usePagedList` (`@/shared/lib`, "Cargar más" over `useInfiniteQuery`); design-bounded sets (household, minors, one event's activities, sponsors) are fetched in a single call with `pageSize: 100` and a server `sort`. Never re-filter/re-sort/slice fetched lists in components — add the missing query param to the backend instead.

## Routing & auth

- Guards are **per-route `beforeEnter`**, not global — `requireAuth`, `requireAdmin`, `redirectIfAuthenticated` in `src/features/auth/model/guards.ts`. The `adminRoute()` helper in `routes.ts` applies `requireAdmin` + the admin layout; all admin routes are lazy-imported. The two print pages (event badges, event roster) declare `requireAdmin` inline with `layout: 'blank'` instead of going through `adminRoute()`.
- Session state is a **module-level singleton** (no Pinia): `src/entities/session/model/session.ts`. `useSession().resolve()` lazily calls `GET /api/auth/me` (deduped in-flight); `App.vue` bootstraps it on mount. Auth is a server-set cookie; only the theme is stored in `localStorage`.

## Theming & styles

- Light/dark is custom, CSS-variable driven: `:root` holds light tokens, the `.ca-dark` class on `<html>` holds dark tokens (`src/assets/styles/variables.css`, all tokens `--ca-*`). `useTheme()` (`src/shared/lib/use-theme.ts`) toggles the class and persists `localStorage['ca-theme']`. Quirk: in dark mode `--ca-orange` is remapped to cyan.
- **The pre-paint theme bootstrap is an external file, `public/theme-init.js`, loaded by `<script src="/theme-init.js">` in `index.html` — do not inline it.** It is external on purpose so the CSP served by nginx (`frontend/docker/security-headers.conf`) can stay `script-src 'self'` with no hash or `'unsafe-inline'`; inlining it silently breaks the theme in production.
- PrimeVue 4 uses a customized Aura preset (`src/app/providers/primevue.ts`, `darkModeSelector: '.ca-dark'`), re-skinned by mapping `--p-*` tokens to `--ca-*` in `src/assets/styles/primevue-overrides.css`.
- All component styles are `<style scoped>`; global styles live only in `src/assets/styles/` (`main.css`, `variables.css`, `primevue-overrides.css`, `richtext.css` — the last one styles rendered rich-text HTML, which is not scopeable).

## SEO

- `src/shared/lib/use-seo.ts` owns every head tag: `applyRouteSeo(to)` runs in `router.afterEach` (only when the navigation did not fail) from static `meta.seo` (`titleKey`/`descriptionKey`/`noindex`, i18n keys under `seo.*`); pages with dynamic data additionally call `useSeo(() => ({...}))` to layer title/description/image/`type`/`jsonLd` on top. Anything with `meta.layout === 'admin'` is forced `noindex`.
- It sets `document.title`, description, canonical (dropped when `noindex`), the `og:*`/`twitter:*` pairs and a single `<script type="application/ld+json" id="ca-jsonld">`. `index.html` only carries the static defaults for crawlers that do not run JS — since the app is client-rendered, per-route OG tags are invisible to non-JS scrapers.
- `/sitemap.xml` and `/robots.txt` are served by the **backend**; Vite (`server.proxy`) and nginx rewrite them to `/api/…`.

## Build & bundling

- Vite 8 bundles with Rolldown. `vite.config.ts` sets `build.rolldownOptions.output.codeSplitting.groups`: explicit, dependency-ordered vendor chunks (`datatable`, `primetheme`, `primevue`, `editor`, `charts`) matched by `node_modules` path regex, plus `chunkSizeWarningLimit: 700`.
- **Do not replace these path groups with a size-based split** (`maxSize`/`minSize`). A size-based split produced circular chunks: a component resolved as `undefined`, Vue fell back to `resolveComponent` by string, slots evaluated eagerly and admin pages rendered blank in production while the dev server looked fine. Add or reorder a path group instead.

## Rich text, charts and the table kit

- Rich text is **TipTap 3** and the stored value is **serialized ProseMirror JSON**, not HTML. `src/shared/lib/richtext.ts` is the single source of extensions (`richTextExtensions()`: StarterKit, TextStyle/Color/Highlight/Underline, Link, TextAlign, tables, image) plus `parseRichText`/`serializeRichText`/`renderRichTextHtml`/`richTextExcerpt`/`isRichTextEmpty`. Authoring uses `RichTextEditor` (`@tiptap/vue-3`, toolbar image upload → `POST /api/files`); display uses `RichTextContent`, which renders `generateHTML` output through `v-html` with no editor instance. Its `Image` extension is wrapped to reject non-same-origin `src` at parse time because the CSP is `img-src 'self' data: blob:` — pasted external images would save fine and render broken for everyone.
- Charts are Chart.js 4 via PrimeVue's `Chart` component, only on the admin dashboard (`src/pages/admin/dashboard/`): `model/charts.ts` builds the `ChartData`/`ChartOptions`, and `useChartTheme()` (`@/shared/lib`) reads the resolved `--ca-*` CSS variables into a `ChartPalette` that recomputes when the theme flips. Never hardcode chart colours — take them from the palette.
- Admin table controls are shared components in `@/shared/ui`: `ColumnSearch` (debounced text/number), `ColumnFilterSelect`, `ColumnFilterDate` (day range) — all Popover-based, `v-model` + an `apply` event — wired to `useServerTable`'s `filters`/`columns` map (`type: 'text' | 'number' | 'dateRange'`, with `fromParam`/`toParam` for ranges). `DataState` renders the loading/empty/error states.

## Conventions

- TypeScript is very strict (`noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`, `verbatimModuleSyntax`, `noUnusedLocals/Parameters`) — it rejects code that would compile in a looser project; expect it to bite.
- Composable file naming: **features** use camelCase (`useLogin.ts`); **entities and `shared/lib`** use kebab-case (`use-theme.ts`, `use-thumbnail-upload.ts`).
