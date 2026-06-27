import type { RouteRecordRaw } from 'vue-router'

import { redirectIfAuthenticated } from '@/modules/auth/presentation/guards'

export const authRoutes: readonly RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/modules/auth/presentation/pages/LoginPage.vue'),
    beforeEnter: () => redirectIfAuthenticated(),
  },
]
