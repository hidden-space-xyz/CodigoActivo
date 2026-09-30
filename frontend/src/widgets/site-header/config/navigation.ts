import type { TranslationKey } from '@/shared/i18n'
import type { NavSection, RouteName } from '@/shared/routes'

/** Link of the public header, active while a route of its `section` is open. */
interface NavItem {
  readonly routeName: RouteName
  readonly section: NavSection
  readonly labelKey: TranslationKey
}

/** Public site header links in display order. */
export const PRIMARY_NAV = [
  { routeName: 'home', section: 'home', labelKey: 'widgets.siteHeader.nav.home' },
  { routeName: 'news', section: 'news', labelKey: 'widgets.siteHeader.nav.news' },
  { routeName: 'events', section: 'events', labelKey: 'widgets.siteHeader.nav.events' },
  { routeName: 'resources', section: 'resources', labelKey: 'widgets.siteHeader.nav.resources' },
  { routeName: 'about', section: 'about', labelKey: 'widgets.siteHeader.nav.about' },
] as const satisfies readonly NavItem[]
