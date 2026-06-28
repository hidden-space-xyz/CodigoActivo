import type { RouteLocationNormalized, RouteRecordRaw } from 'vue-router'

import { requireAuth } from '@/modules/auth/presentation/guards'

function adminRoute(
  path: string,
  name: string,
  component: RouteRecordRaw['component'],
): RouteRecordRaw {
  return {
    path,
    name,
    component,
    meta: { layout: 'admin' },
    beforeEnter: (to: RouteLocationNormalized) => requireAuth(to),
  } as RouteRecordRaw
}

export const adminRoutes: readonly RouteRecordRaw[] = [
  { path: '/admin', redirect: { name: 'admin-dashboard' } },
  adminRoute(
    '/admin/dashboard',
    'admin-dashboard',
    () => import('@/features/dashboard/DashboardPage.vue'),
  ),
  adminRoute(
    '/admin/events',
    'admin-events',
    () => import('@/features/events/EventsAdminPage.vue'),
  ),
  adminRoute(
    '/admin/events/:eventId',
    'admin-event-detail',
    () => import('@/features/events/EventDetailPage.vue'),
  ),
  adminRoute(
    '/admin/events/:eventId/activities/:activityId',
    'admin-activity-detail',
    () => import('@/features/activities/ActivityAssignmentsPage.vue'),
  ),
  adminRoute(
    '/admin/announcements',
    'admin-announcements',
    () => import('@/features/announcements/AnnouncementsPage.vue'),
  ),
  adminRoute(
    '/admin/partners',
    'admin-partners',
    () => import('@/features/partners/PartnersPage.vue'),
  ),
  adminRoute(
    '/admin/resources',
    'admin-resources',
    () => import('@/features/resources/ResourcesAdminPage.vue'),
  ),
  adminRoute('/admin/users', 'admin-users', () => import('@/features/users/UsersPage.vue')),
  adminRoute(
    '/admin/catalogs',
    'admin-catalogs',
    () => import('@/features/catalogs/CatalogsPage.vue'),
  ),
]
