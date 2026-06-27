export interface NavItem {
  readonly routeName: string
  readonly label: string
}

export const PRIMARY_NAV: readonly NavItem[] = [
  { routeName: 'home', label: 'Inicio' },
  { routeName: 'about', label: 'Nosotros' },
  { routeName: 'events', label: 'Eventos' },
  { routeName: 'resources', label: 'Recursos' },
  { routeName: 'announcements', label: 'Anuncios' },
]
