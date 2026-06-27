import type { RouteRecordRaw } from 'vue-router'

export const resourcesRoutes: readonly RouteRecordRaw[] = [
  {
    path: '/resources',
    name: 'resources',
    component: () => import('@/modules/resources/presentation/pages/ResourcesPage.vue'),
  },
]
