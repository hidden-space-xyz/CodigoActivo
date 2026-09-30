import type { QueryClient } from '@tanstack/vue-query'
import { createRouter, createWebHistory, type Router, type RouterHistory } from 'vue-router'

import { queryClient } from '@/app/config/query-client'
import { applyRouteSeo } from '@/shared/lib/seo'

import { installAccessGuard } from './guards'
import { routes } from './routes'

/** What the application router is built on: its history and the query client of the session. */
interface AppRouterOptions {
  readonly history: RouterHistory
  readonly queryClient: QueryClient
}

/**
 * Router over the application routes. It restores the saved scroll position on back/forward and
 * otherwise scrolls smoothly to the top, checks `meta.access` before every navigation and applies
 * the route SEO after each successful one.
 */
export function createAppRouter(options: AppRouterOptions): Router {
  const router = createRouter({
    history: options.history,
    routes: [...routes],
    scrollBehavior(_to, _from, savedPosition) {
      if (savedPosition) return savedPosition
      return { top: 0, behavior: 'smooth' }
    },
  })

  installAccessGuard(router, options.queryClient)
  router.afterEach((to, _from, failure) => {
    if (!failure) applyRouteSeo(to)
  })

  return router
}

/** The application's history-mode router. */
export const router = createAppRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  queryClient,
})
