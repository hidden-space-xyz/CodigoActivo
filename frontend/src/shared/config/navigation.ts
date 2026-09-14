import type { TranslationKey } from '@/shared/i18n'

interface NavItem {
  readonly routeName: string
  readonly labelKey: TranslationKey
}

export const PRIMARY_NAV: readonly NavItem[] = [
  { routeName: 'home', labelKey: 'nav.home' },
  { routeName: 'announcements', labelKey: 'nav.announcements' },
  { routeName: 'events', labelKey: 'nav.events' },
  { routeName: 'resources', labelKey: 'nav.resources' },
  { routeName: 'about', labelKey: 'nav.about' },
]
