import type { TranslationKey } from '@/shared/i18n'

/** Entry of the admin sidebar menu. */
export interface AdminNavItem {
  readonly labelKey: TranslationKey
  readonly routeName: string
  readonly icon: string
}

/** Admin sidebar entries in display order; `routeName` must match a named route in the router. */
export const ADMIN_NAV: readonly AdminNavItem[] = [
  { labelKey: 'adminNav.dashboard', routeName: 'admin-dashboard', icon: 'chart-bar' },
  { labelKey: 'adminNav.events', routeName: 'admin-events', icon: 'calendar' },
  { labelKey: 'adminNav.news', routeName: 'admin-news', icon: 'megaphone' },
  { labelKey: 'adminNav.partners', routeName: 'admin-partners', icon: 'building' },
  { labelKey: 'adminNav.resources', routeName: 'admin-resources', icon: 'book' },
  { labelKey: 'adminNav.users', routeName: 'admin-users', icon: 'users' },
  { labelKey: 'adminNav.settings', routeName: 'admin-catalogs', icon: 'cog' },
]
