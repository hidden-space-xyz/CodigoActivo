import { createRouter, createWebHistory } from 'vue-router'

import { applyRouteSeo } from '@/shared/lib'

import { routes } from './routes'

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
