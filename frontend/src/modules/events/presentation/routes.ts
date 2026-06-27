import type { RouteRecordRaw } from 'vue-router'

export const eventsRoutes: readonly RouteRecordRaw[] = [
  {
    path: '/events',
    name: 'events',
    component: () => import('@/modules/events/presentation/pages/EventsPage.vue'),
  },
  {
    path: '/events/:eventId',
    name: 'event-detail',
    component: () => import('@/modules/events/presentation/pages/EventDetailPage.vue'),
    props: true,
  },
]
