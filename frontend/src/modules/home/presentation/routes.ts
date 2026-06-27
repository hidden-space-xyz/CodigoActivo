import type { RouteRecordRaw } from 'vue-router'

export const homeRoutes: readonly RouteRecordRaw[] = [
  {
    path: '/',
    name: 'home',
    component: () => import('@/modules/home/presentation/pages/HomePage.vue'),
  },
]
