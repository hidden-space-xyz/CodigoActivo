import type { RouteRecordRaw } from 'vue-router'

export const aboutRoutes: readonly RouteRecordRaw[] = [
  {
    path: '/about',
    name: 'about',
    component: () => import('@/modules/about/presentation/pages/AboutPage.vue'),
  },
]
