import { defineComponent, h, type Component } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { VueQueryPlugin, type QueryClient } from '@tanstack/vue-query'
import {
  createMemoryHistory,
  createRouter,
  type RouteLocationRaw,
  type Router,
  type RouteRecordRaw,
} from 'vue-router'

import App from '@/app/App.vue'
import { elementPlus } from '@/app/config/element-plus'
import { createAppRouter as createRouterForApp } from '@/app/router'
import { routes as appRoutes } from '@/app/router/routes'
import { startSession, type AuthUser } from '@/entities/session'
import { createQueryClient } from '@/shared/api'
import { i18n } from '@/shared/i18n'

import { buildAuthUser } from './models'

/** Query client without retries, so failures surface immediately. */
export function createTestQueryClient(): QueryClient {
  return createQueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: Infinity, staleTime: 0 },
      mutations: { retry: false },
    },
  })
}

const RouteStub: Component = {
  name: 'RouteStub',
  render: () => h('div', { 'data-testid': 'route-stub' }),
}

/**
 * Memory router with the application's route names and paths but stub components and no guards,
 * so `RouterLink`s and `router.push` calls inside isolated components resolve without loading pages.
 */
function createStubRouter(extraRoutes: RouteRecordRaw[] = []): Router {
  const stubbed = appRoutes.map((route): RouteRecordRaw => {
    if ('redirect' in route && route.redirect) return route
    return {
      path: route.path,
      ...(route.name ? { name: route.name } : {}),
      ...(route.meta ? { meta: route.meta } : {}),
      component: RouteStub,
    }
  })
  return createRouter({ history: createMemoryHistory(), routes: [...extraRoutes, ...stubbed] })
}

/** Memory router with the real application routes, lazy pages, access guard and SEO hook. */
export function createAppRouter(queryClient: QueryClient): Router {
  return createRouterForApp({ history: createMemoryHistory(), queryClient })
}

function plugins(queryClient: QueryClient, router: Router) {
  return [
    i18n,
    [elementPlus.plugin, elementPlus.options],
    [VueQueryPlugin, { queryClient }],
    router,
  ] as const
}

/** Options for `renderWithProviders`. */
export interface RenderOptions {
  readonly props?: Record<string, unknown>
  readonly slots?: Record<string, unknown>
  /** Initial location; defaults to `/`. */
  readonly route?: RouteLocationRaw
  /** Router to use; defaults to `createStubRouter()`. */
  readonly router?: Router
  /** Signed-in user; partial values are completed by `buildAuthUser`. Omit it for a guest. */
  readonly user?: Partial<AuthUser>
  /** Query client to use; defaults to a fresh `createTestQueryClient()`. */
  readonly queryClient?: QueryClient
  /** Extra `global.stubs`. */
  readonly stubs?: Record<string, Component | boolean | string>
  /** Attach the component to `document.body` (focus handling, DOM queries on teleports). */
  readonly attach?: boolean
}

/**
 * Mounts a component with the same plugins as the app (i18n, Element Plus, TanStack Query and a
 * router), waits for the initial navigation and pending promises, and returns the wrapper with the
 * router and query client it used.
 */
export async function renderWithProviders(component: Component, options: RenderOptions = {}) {
  const queryClient = options.queryClient ?? createTestQueryClient()
  const router = options.router ?? createStubRouter()

  if (options.user) startSession(queryClient, buildAuthUser(options.user))

  await router.push(options.route ?? '/')
  await router.isReady()

  const wrapper = mount(component, {
    ...(options.props ? { props: options.props } : {}),
    ...(options.slots ? { slots: options.slots } : {}),
    ...(options.attach ? { attachTo: document.body } : {}),
    global: {
      plugins: [...plugins(queryClient, router)],
      ...(options.stubs ? { stubs: options.stubs } : {}),
    },
  } as never)

  await flushPromises()

  return { wrapper, router, queryClient }
}

/**
 * Runs `composable` inside the `setup` of a throwaway component mounted with the app providers, so
 * it can use `useI18n`, the router or TanStack Query. Returns the composable's result together with
 * the wrapper, router and query client (for spying on invalidations).
 */
export async function mountComposable<T>(composable: () => T, options: RenderOptions = {}) {
  const box: { value?: T } = {}
  const Host = defineComponent({
    name: 'ComposableHost',
    setup() {
      box.value = composable()
      return () => h('div')
    },
  })
  const rendered = await renderWithProviders(Host, options)
  if (!('value' in box)) throw new Error('Composable did not run')
  return { result: box.value, ...rendered }
}

/**
 * Mounts the whole application (`App.vue` with layouts and the real route table) at `path`: the
 * closest a test gets to a browser session. The API is still served by the MSW mock server.
 */
export async function renderApp(path: string, options: { user?: Partial<AuthUser> } = {}) {
  const queryClient = createTestQueryClient()
  const router = createAppRouter(queryClient)

  if (options.user) startSession(queryClient, buildAuthUser(options.user))

  await router.push(path)
  await router.isReady()

  const wrapper = mount(App, {
    attachTo: document.body,
    global: { plugins: [...plugins(queryClient, router)] },
  } as never)

  await flushPromises()

  return { wrapper, router, queryClient }
}

/** Translates an i18n key exactly as components do, to assert on rendered text. */
export function t(key: string, values?: Record<string, unknown>): string {
  const translate = i18n.global.t as (key: string, values?: Record<string, unknown>) => string
  return values ? translate(key, values) : translate(key)
}
