import type { RouteLocationNormalized, RouteRecordRaw } from 'vue-router'

import { redirectIfAuthenticated, requireAdmin, requireAuth } from '@/features/auth'

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
    beforeEnter: (to: RouteLocationNormalized) => requireAdmin(to),
  } as RouteRecordRaw
}

export const routes: readonly RouteRecordRaw[] = [
  { path: '/', name: 'home', component: () => import('@/pages/home').then((m) => m.HomePage) },
  {
    path: '/about',
    name: 'about',
    component: () => import('@/pages/about').then((m) => m.AboutPage),
  },
  {
    path: '/events',
    name: 'events',
    component: () => import('@/pages/events').then((m) => m.EventsPage),
  },
  {
    path: '/events/:eventId',
    name: 'event-detail',
    component: () => import('@/pages/event-detail').then((m) => m.EventDetailPage),
    props: true,
  },
  {
    path: '/resources',
    name: 'resources',
    component: () => import('@/pages/resources').then((m) => m.ResourcesPage),
  },
  {
    path: '/resources/:resourceId',
    name: 'resource-detail',
    component: () => import('@/pages/resource-detail').then((m) => m.ResourceDetailPage),
    props: true,
  },
  {
    path: '/announcements',
    name: 'announcements',
    component: () => import('@/pages/announcements').then((m) => m.AnnouncementsPage),
  },
  {
    path: '/announcements/:announcementId',
    name: 'announcement-detail',
    component: () => import('@/pages/announcement-detail').then((m) => m.AnnouncementDetailPage),
    props: true,
  },
  {
    path: '/register',
    name: 'register',
    component: () => import('@/pages/register').then((m) => m.RegisterPage),
    beforeEnter: () => redirectIfAuthenticated(),
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('@/pages/login').then((m) => m.LoginPage),
    beforeEnter: () => redirectIfAuthenticated(),
  },
  {
    path: '/verify-account',
    name: 'verify-account',
    component: () => import('@/pages/verify-account').then((m) => m.VerifyAccountPage),
  },
  {
    path: '/account',
    name: 'account',
    component: () => import('@/pages/account').then((m) => m.AccountPage),
    beforeEnter: (to: RouteLocationNormalized) => requireAuth(to),
  },
  { path: '/admin', redirect: { name: 'admin-dashboard' } },
  adminRoute('/admin/dashboard', 'admin-dashboard', () =>
    import('@/pages/admin/dashboard').then((m) => m.DashboardPage),
  ),
  adminRoute('/admin/events', 'admin-events', () =>
    import('@/pages/admin/events').then((m) => m.EventsAdminPage),
  ),
  adminRoute('/admin/events/:eventId', 'admin-event-detail', () =>
    import('@/pages/admin/event-detail').then((m) => m.EventDetailPage),
  ),
  adminRoute('/admin/events/:eventId/activities/:activityId', 'admin-activity-detail', () =>
    import('@/pages/admin/activity-assignments').then((m) => m.ActivityAssignmentsPage),
  ),
  {
    // Printable badge sheet: admin-only but rendered without the admin chrome so the
    // printed pages contain nothing but the labels.
    path: '/admin/events/:eventId/badges',
    name: 'admin-event-badges',
    component: () => import('@/pages/admin/event-badges').then((m) => m.EventBadgesPage),
    meta: { layout: 'blank' },
    beforeEnter: (to: RouteLocationNormalized) => requireAdmin(to),
  },
  adminRoute('/admin/announcements', 'admin-announcements', () =>
    import('@/pages/admin/announcements').then((m) => m.AnnouncementsPage),
  ),
  adminRoute('/admin/partners', 'admin-partners', () =>
    import('@/pages/admin/partners').then((m) => m.PartnersPage),
  ),
  adminRoute('/admin/resources', 'admin-resources', () =>
    import('@/pages/admin/resources').then((m) => m.ResourcesPage),
  ),
  adminRoute('/admin/users', 'admin-users', () =>
    import('@/pages/admin/users').then((m) => m.UsersPage),
  ),
  adminRoute('/admin/catalogs', 'admin-catalogs', () =>
    import('@/pages/admin/catalogs').then((m) => m.CatalogsPage),
  ),
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    redirect: { name: 'home' },
  },
]
