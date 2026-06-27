import type { RouteRecordRaw } from 'vue-router'

import { adminRoutes } from '@/features/admin/router/admin.routes'
import { aboutRoutes } from '@/modules/about/presentation/routes'
import { authRoutes } from '@/modules/auth/presentation/routes'
import { eventsRoutes } from '@/modules/events/presentation/routes'
import { homeRoutes } from '@/modules/home/presentation/routes'
import { registrationRoutes } from '@/modules/registration/presentation/routes'
import { resourcesRoutes } from '@/modules/resources/presentation/routes'

export const routes: readonly RouteRecordRaw[] = [
  ...homeRoutes,
  ...aboutRoutes,
  ...eventsRoutes,
  ...resourcesRoutes,
  ...registrationRoutes,
  ...authRoutes,
  ...adminRoutes,
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    redirect: { name: 'home' },
  },
]
