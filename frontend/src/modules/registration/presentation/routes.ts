import type { RouteRecordRaw } from 'vue-router'

import { redirectIfAuthenticated } from '@/modules/auth/presentation/guards'

export const registrationRoutes: readonly RouteRecordRaw[] = [
  {
    path: '/register',
    name: 'register',
    component: () => import('@/modules/registration/presentation/pages/RegisterPage.vue'),
    beforeEnter: () => redirectIfAuthenticated(),
  },
]
