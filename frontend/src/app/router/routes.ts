import type { RouteLocationNormalized, RouteRecordRaw } from 'vue-router'

import { redirectIfAuthenticated, requireAdmin, requireAuth } from '@/features/auth'
import type { SeoRouteMeta } from '@/shared/lib'

declare module 'vue-router' {
  interface RouteMeta {
    layout?: 'admin' | 'blank' | undefined
    seo?: SeoRouteMeta | undefined
  }
}

function adminRoute(
  path: string,
  name: string,
  component: RouteRecordRaw['component'],
  meta?: RouteRecordRaw['meta'],
): RouteRecordRaw {
  return {
    path,
    name,
    component,
    meta: { layout: 'admin', ...meta },
    beforeEnter: (to: RouteLocationNormalized) => requireAdmin(to),
  } as RouteRecordRaw
}

/**
 * Application route table with lazily loaded pages. Admin routes use the admin layout (or `blank`
 * for printable pages) behind `requireAdmin`; public detail routes receive their path params as
 * props.
 */
export const routes: readonly RouteRecordRaw[] = [
  { path: '/', name: 'home', component: () => import('@/pages/home').then((m) => m.HomePage) },
  {
    path: '/about',
    name: 'about',
    component: () => import('@/pages/about').then((m) => m.AboutPage),
    meta: {
      seo: {
        titleKey: 'seo.routes.about.title',
        descriptionKey: 'seo.routes.about.description',
      },
    },
  },
  {
    path: '/events',
    name: 'events',
    component: () => import('@/pages/events').then((m) => m.EventsPage),
    meta: {
      seo: {
        titleKey: 'seo.routes.events.title',
        descriptionKey: 'seo.routes.events.description',
      },
    },
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
    meta: {
      seo: {
        titleKey: 'seo.routes.resources.title',
        descriptionKey: 'seo.routes.resources.description',
      },
    },
  },
  {
    path: '/resources/:resourceId',
    name: 'resource-detail',
    component: () => import('@/pages/resource-detail').then((m) => m.ResourceDetailPage),
    props: true,
  },
  {
    path: '/news',
    name: 'news',
    component: () => import('@/pages/news').then((m) => m.NewsPage),
    meta: {
      seo: {
        titleKey: 'seo.routes.news.title',
        descriptionKey: 'seo.routes.news.description',
      },
    },
  },
  {
    path: '/news/:newsItemId',
    name: 'news-detail',
    component: () => import('@/pages/news-detail').then((m) => m.NewsDetailPage),
    props: true,
  },
  {
    path: '/cookies',
    name: 'cookie-policy',
    component: () => import('@/pages/cookie-policy').then((m) => m.CookiePolicyPage),
    meta: {
      seo: {
        titleKey: 'seo.routes.cookiePolicy.title',
        descriptionKey: 'seo.routes.cookiePolicy.description',
      },
    },
  },
  {
    path: '/register',
    name: 'register',
    component: () => import('@/pages/register').then((m) => m.RegisterPage),
    beforeEnter: () => redirectIfAuthenticated(),
    meta: {
      seo: {
        titleKey: 'seo.routes.register.title',
        descriptionKey: 'seo.routes.register.description',
      },
    },
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('@/pages/login').then((m) => m.LoginPage),
    beforeEnter: () => redirectIfAuthenticated(),
    meta: { seo: { titleKey: 'seo.routes.login.title', noindex: true } },
  },
  {
    path: '/login/verify',
    name: 'login-two-factor',
    component: () => import('@/pages/login-two-factor').then((m) => m.TwoFactorLoginPage),
    beforeEnter: () => redirectIfAuthenticated(),
    meta: { seo: { titleKey: 'seo.routes.loginTwoFactor.title', noindex: true } },
  },
  {
    path: '/verify-account',
    name: 'verify-account',
    component: () => import('@/pages/verify-account').then((m) => m.VerifyAccountPage),
    meta: { seo: { titleKey: 'seo.routes.verifyAccount.title', noindex: true } },
  },
  {
    path: '/forgot-password',
    name: 'forgot-password',
    component: () => import('@/pages/forgot-password').then((m) => m.ForgotPasswordPage),
    beforeEnter: () => redirectIfAuthenticated(),
    meta: { seo: { titleKey: 'seo.routes.forgotPassword.title', noindex: true } },
  },
  {
    path: '/reset-password',
    name: 'reset-password',
    component: () => import('@/pages/reset-password').then((m) => m.ResetPasswordPage),
    meta: { seo: { titleKey: 'seo.routes.resetPassword.title', noindex: true } },
  },
  {
    path: '/account',
    name: 'account',
    component: () => import('@/pages/account').then((m) => m.AccountPage),
    beforeEnter: (to: RouteLocationNormalized) => requireAuth(to),
    meta: { seo: { titleKey: 'seo.routes.account.title', noindex: true } },
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
  adminRoute(
    '/admin/events/:eventId/badges',
    'admin-event-badges',
    () => import('@/pages/admin/event-badges').then((m) => m.EventBadgesPage),
    { layout: 'blank', seo: { titleKey: 'seo.routes.eventBadges.title', noindex: true } },
  ),
  adminRoute(
    '/admin/events/:eventId/roster',
    'admin-event-roster',
    () => import('@/pages/admin/event-roster').then((m) => m.EventRosterPage),
    { layout: 'blank', seo: { titleKey: 'seo.routes.eventRoster.title', noindex: true } },
  ),
  adminRoute('/admin/news', 'admin-news', () =>
    import('@/pages/admin/news').then((m) => m.NewsPage),
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
    component: () => import('@/pages/not-found').then((m) => m.NotFoundPage),
    meta: { seo: { titleKey: 'seo.routes.notFound.title', noindex: true } },
  },
]
