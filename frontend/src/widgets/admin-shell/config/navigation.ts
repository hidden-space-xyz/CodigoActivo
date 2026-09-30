import type { TranslationKey } from '@/shared/i18n'
import type { RouteName } from '@/shared/routes'

/** Entry of the admin sidebar menu. */
interface AdminNavItem {
  readonly labelKey: TranslationKey
  readonly routeName: RouteName
  readonly icon: string
}

/** Admin sidebar entries in display order. */
export const ADMIN_NAV = [
  { labelKey: 'widgets.adminShell.nav.dashboard', routeName: 'admin-dashboard', icon: 'chart-bar' },
  { labelKey: 'widgets.adminShell.nav.events', routeName: 'admin-events', icon: 'calendar' },
  { labelKey: 'widgets.adminShell.nav.news', routeName: 'admin-news', icon: 'megaphone' },
  { labelKey: 'widgets.adminShell.nav.partners', routeName: 'admin-partners', icon: 'building' },
  { labelKey: 'widgets.adminShell.nav.resources', routeName: 'admin-resources', icon: 'book' },
  { labelKey: 'widgets.adminShell.nav.users', routeName: 'admin-users', icon: 'users' },
  { labelKey: 'widgets.adminShell.nav.settings', routeName: 'admin-catalogs', icon: 'cog' },
] as const satisfies readonly AdminNavItem[]
