import type { StaffAccess } from '@/entities/session'
import type { TranslationKey } from '@/shared/i18n'
import type { RouteName } from '@/shared/routes'

/** Entry of the admin sidebar menu, shown only to those with the `access` its page needs. */
interface AdminNavItem {
  readonly labelKey: TranslationKey
  readonly routeName: RouteName
  readonly icon: string
  readonly access: StaffAccess
}

/** Admin sidebar entries in display order. */
export const ADMIN_NAV = [
  {
    labelKey: 'widgets.adminShell.nav.dashboard',
    routeName: 'admin-dashboard',
    icon: 'chart-bar',
    access: 'admin',
  },
  {
    labelKey: 'widgets.adminShell.nav.events',
    routeName: 'admin-events',
    icon: 'calendar',
    access: 'editor',
  },
  {
    labelKey: 'widgets.adminShell.nav.news',
    routeName: 'admin-news',
    icon: 'megaphone',
    access: 'editor',
  },
  {
    labelKey: 'widgets.adminShell.nav.partners',
    routeName: 'admin-partners',
    icon: 'building',
    access: 'editor',
  },
  {
    labelKey: 'widgets.adminShell.nav.resources',
    routeName: 'admin-resources',
    icon: 'book',
    access: 'editor',
  },
  {
    labelKey: 'widgets.adminShell.nav.users',
    routeName: 'admin-users',
    icon: 'users',
    access: 'admin',
  },
  {
    labelKey: 'widgets.adminShell.nav.inventory',
    routeName: 'admin-inventory',
    icon: 'box',
    access: 'admin',
  },
  {
    labelKey: 'widgets.adminShell.nav.diagnostics',
    routeName: 'admin-diagnostics',
    icon: 'list',
    access: 'admin',
  },
  {
    labelKey: 'widgets.adminShell.nav.audit',
    routeName: 'admin-audit',
    icon: 'check-square',
    access: 'audit',
  },
  {
    labelKey: 'widgets.adminShell.nav.blockedData',
    routeName: 'admin-blocked-data',
    icon: 'lock',
    access: 'admin',
  },
  {
    labelKey: 'widgets.adminShell.nav.legal',
    routeName: 'admin-legal-policies',
    icon: 'file',
    access: 'admin',
  },
  {
    labelKey: 'widgets.adminShell.nav.catalogs',
    routeName: 'admin-catalogs',
    icon: 'cog',
    access: 'editor',
  },
] as const satisfies readonly AdminNavItem[]
