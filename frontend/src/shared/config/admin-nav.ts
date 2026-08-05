export interface AdminNavItem {
  readonly labelKey: string
  readonly routeName: string
  readonly icon: string
}

export const ADMIN_NAV: readonly AdminNavItem[] = [
  { labelKey: 'adminNav.dashboard', routeName: 'admin-dashboard', icon: 'chart-bar' },
  { labelKey: 'adminNav.events', routeName: 'admin-events', icon: 'calendar' },
  { labelKey: 'adminNav.announcements', routeName: 'admin-announcements', icon: 'megaphone' },
  { labelKey: 'adminNav.partners', routeName: 'admin-partners', icon: 'building' },
  { labelKey: 'adminNav.resources', routeName: 'admin-resources', icon: 'book' },
  { labelKey: 'adminNav.users', routeName: 'admin-users', icon: 'users' },
  { labelKey: 'adminNav.settings', routeName: 'admin-catalogs', icon: 'cog' },
]
