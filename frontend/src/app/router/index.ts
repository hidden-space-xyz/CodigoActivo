import { createRouter, createWebHistory } from 'vue-router'

import { applyRouteSeo } from '@/shared/lib'

import { routes } from './routes'

/**
 * History-mode router. Restores the saved scroll position on back/forward, otherwise scrolls
 * smoothly to the top, and applies route SEO metadata after each successful navigation.
 */
export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [...routes],
  scrollBehavior(_to, _from, savedPosition) {
    if (savedPosition) return savedPosition
    return { top: 0, behavior: 'smooth' }
  },
})

router.afterEach((to, _from, failure) => {
  if (!failure) applyRouteSeo(to)
})
