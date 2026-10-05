import type { ParamValue, ParamValueZeroOrMore, RouteRecordInfo } from 'vue-router'

import type { SeoRouteMeta } from '@/shared/lib/seo'

type Params<Keys extends string, Raw extends boolean> = { [Key in Keys]: ParamValue<Raw> }

type NamedRoute<
  Name extends string,
  Path extends string,
  Keys extends string = never,
> = RouteRecordInfo<Name, Path, Params<Keys, true>, Params<Keys, false>>

/**
 * Every named route of the application with its path and params. `RouterLink`, `router.push` and
 * route guards only accept these names, each with exactly the params its path declares.
 */
interface RouteNamedMap {
  home: NamedRoute<'home', '/'>
  about: NamedRoute<'about', '/about'>
  events: NamedRoute<'events', '/events'>
  'event-detail': NamedRoute<'event-detail', '/events/:eventId', 'eventId'>
  resources: NamedRoute<'resources', '/resources'>
  'resource-detail': NamedRoute<'resource-detail', '/resources/:resourceId', 'resourceId'>
  news: NamedRoute<'news', '/news'>
  'news-detail': NamedRoute<'news-detail', '/news/:newsItemId', 'newsItemId'>
  'cookie-policy': NamedRoute<'cookie-policy', '/cookies'>
  register: NamedRoute<'register', '/register'>
  login: NamedRoute<'login', '/login'>
  'login-two-factor': NamedRoute<'login-two-factor', '/login/verify'>
  'verify-account': NamedRoute<'verify-account', '/verify-account'>
  'confirm-email': NamedRoute<'confirm-email', '/confirm-email'>
  'forgot-password': NamedRoute<'forgot-password', '/forgot-password'>
  'reset-password': NamedRoute<'reset-password', '/reset-password'>
  account: NamedRoute<'account', '/account'>
  'admin-dashboard': NamedRoute<'admin-dashboard', '/admin/dashboard'>
  'admin-events': NamedRoute<'admin-events', '/admin/events'>
  'admin-event-detail': NamedRoute<'admin-event-detail', '/admin/events/:eventId', 'eventId'>
  'admin-event-badges': NamedRoute<'admin-event-badges', '/admin/events/:eventId/badges', 'eventId'>
  'admin-event-roster': NamedRoute<'admin-event-roster', '/admin/events/:eventId/roster', 'eventId'>
  'admin-news': NamedRoute<'admin-news', '/admin/news'>
  'admin-partners': NamedRoute<'admin-partners', '/admin/partners'>
  'admin-resources': NamedRoute<'admin-resources', '/admin/resources'>
  'admin-users': NamedRoute<'admin-users', '/admin/users'>
  'admin-catalogs': NamedRoute<'admin-catalogs', '/admin/catalogs'>
  'not-found': RouteRecordInfo<
    'not-found',
    '/:pathMatch(.*)*',
    { pathMatch?: ParamValueZeroOrMore<true> },
    { pathMatch?: ParamValue<false>[] }
  >
}

/** Name of a route of the application. */
export type RouteName = keyof RouteNamedMap

/** Who may open a route: only guests, any signed-in user, or administrators. */
type RouteAccess = 'guest' | 'user' | 'admin'

/** Section of the public header a route belongs to, highlighted while the route is open. */
export type NavSection = 'home' | 'news' | 'events' | 'resources' | 'about' | 'account'

declare module 'vue-router' {
  interface TypesConfig {
    RouteNamedMap: RouteNamedMap
  }

  interface RouteMeta {
    /** Who may open the route; unset means anyone. */
    access?: RouteAccess | undefined
    /** Layout the page renders in; unset means the public site layout. */
    layout?: 'admin' | 'blank' | undefined
    /** Static SEO resolved on every navigation. */
    seo?: SeoRouteMeta | undefined
    /** Header section highlighted while the route is open. */
    section?: NavSection | undefined
  }
}
