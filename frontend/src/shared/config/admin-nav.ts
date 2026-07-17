interface AdminNavItem {
  readonly labelKey: string
  readonly routeName: string
  readonly icon: string
}

export const ADMIN_NAV: readonly AdminNavItem[] = [
  { labelKey: 'adminNav.dashboard', routeName: 'admin-dashboard', icon: 'pi pi-chart-bar' },
  { labelKey: 'adminNav.events', routeName: 'admin-events', icon: 'pi pi-calendar' },
  { labelKey: 'adminNav.announcements', routeName: 'admin-announcements', icon: 'pi pi-megaphone' },
  { labelKey: 'adminNav.partners', routeName: 'admin-partners', icon: 'pi pi-building' },
  { labelKey: 'adminNav.resources', routeName: 'admin-resources', icon: 'pi pi-book' },
  { labelKey: 'adminNav.users', routeName: 'admin-users', icon: 'pi pi-users' },
  { labelKey: 'adminNav.settings', routeName: 'admin-catalogs', icon: 'pi pi-cog' },
]
