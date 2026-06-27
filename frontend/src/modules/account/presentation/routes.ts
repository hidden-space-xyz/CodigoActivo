import type { RouteLocationNormalized, RouteRecordRaw } from 'vue-router'

import { requireAuth } from '@/modules/auth/presentation/guards'

export const accountRoutes: readonly RouteRecordRaw[] = [
  {
    path: '/cuenta',
    name: 'account',
    component: () => import('@/modules/account/presentation/pages/AccountPage.vue'),
    beforeEnter: (to: RouteLocationNormalized) => requireAuth(to),
  },
]
