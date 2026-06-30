export interface AdminNavItem {
  readonly label: string
  readonly routeName: string
  readonly icon: string
}

export const ADMIN_NAV: readonly AdminNavItem[] = [
  { label: 'Panel', routeName: 'admin-dashboard', icon: 'pi pi-chart-bar' },
  { label: 'Eventos', routeName: 'admin-events', icon: 'pi pi-calendar' },
  { label: 'Anuncios', routeName: 'admin-announcements', icon: 'pi pi-megaphone' },
  { label: 'Patrocinadores', routeName: 'admin-partners', icon: 'pi pi-building' },
  { label: 'Recursos', routeName: 'admin-resources', icon: 'pi pi-book' },
  { label: 'Usuarios', routeName: 'admin-users', icon: 'pi pi-users' },
  { label: 'Configuración', routeName: 'admin-catalogs', icon: 'pi pi-cog' },
]
