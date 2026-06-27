export interface NavItem {
  readonly routeName: string
  readonly label: string
}

export const PRIMARY_NAV: readonly NavItem[] = [
  { routeName: 'home', label: 'Inicio' },
  { routeName: 'announcements', label: 'Noticias' },
  { routeName: 'events', label: 'Eventos' },
  { routeName: 'resources', label: 'Recursos' },
  { routeName: 'about', label: 'Nosotros' },
]
