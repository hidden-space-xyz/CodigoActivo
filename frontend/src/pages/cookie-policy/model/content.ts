import { i18n } from '@/shared/i18n'

interface StorageEntry {
  readonly name: string
  readonly kind: string
  readonly purpose: string
  readonly duration: string
}

interface BrowserGuide {
  readonly label: string
  readonly url: string
}

const ENTRIES: readonly StorageEntry[] = [
  {
    name: '__Host-CodigoActivo.Session',
    kind: i18n.global.t('pages.cookiePolicy.used.kinds.cookie'),
    purpose: i18n.global.t('pages.cookiePolicy.used.session.purpose'),
    duration: i18n.global.t('pages.cookiePolicy.used.session.duration'),
  },
  {
    name: '__Host-CodigoActivo.TwoFactor',
    kind: i18n.global.t('pages.cookiePolicy.used.kinds.cookie'),
    purpose: i18n.global.t('pages.cookiePolicy.used.twoFactor.purpose'),
    duration: i18n.global.t('pages.cookiePolicy.used.twoFactor.duration'),
  },
  {
    name: '__Host-CodigoActivo.Csrf',
    kind: i18n.global.t('pages.cookiePolicy.used.kinds.cookie'),
    purpose: i18n.global.t('pages.cookiePolicy.used.csrf.purpose'),
    duration: i18n.global.t('pages.cookiePolicy.used.csrf.duration'),
  },
  {
    name: 'ca-theme',
    kind: i18n.global.t('pages.cookiePolicy.used.kinds.localStorage'),
    purpose: i18n.global.t('pages.cookiePolicy.used.theme.purpose'),
    duration: i18n.global.t('pages.cookiePolicy.used.theme.duration'),
  },
  {
    name: 'ca:stale-build-reload',
    kind: i18n.global.t('pages.cookiePolicy.used.kinds.sessionStorage'),
    purpose: i18n.global.t('pages.cookiePolicy.used.staleBuild.purpose'),
    duration: i18n.global.t('pages.cookiePolicy.used.staleBuild.duration'),
  },
]

const BROWSER_GUIDES: readonly BrowserGuide[] = [
  {
    label: i18n.global.t('pages.cookiePolicy.manage.chrome'),
    url: 'https://support.google.com/chrome/answer/95647?hl=es',
  },
  {
    label: i18n.global.t('pages.cookiePolicy.manage.firefox'),
    url: 'https://support.mozilla.org/es/kb/Borrar%20cookies',
  },
  {
    label: i18n.global.t('pages.cookiePolicy.manage.safariIos'),
    url: 'https://support.apple.com/es-es/105082',
  },
  {
    label: i18n.global.t('pages.cookiePolicy.manage.safariMac'),
    url: 'https://support.apple.com/es-es/guide/safari/sfri11471/mac',
  },
  {
    label: i18n.global.t('pages.cookiePolicy.manage.edge'),
    url: 'https://support.microsoft.com/es-es/microsoft-edge/eliminar-las-cookies-en-microsoft-edge-63947406-40ac-c3b8-57b9-2a946a29ae09',
  },
]

/**
 * Static content of the cookie policy: every cookie and Web Storage key the site writes (production
 * cookie names, which carry the `__Host-` prefix), browser help pages for deleting them and the
 * Spanish data protection authority links. Texts are translated once at module load.
 */
export function useCookiePolicyContent() {
  return {
    entries: ENTRIES,
    browserGuides: BROWSER_GUIDES,
    authorityUrl: 'https://www.aepd.es',
    cookieGuideUrl: 'https://www.aepd.es/guias/guia-cookies.pdf',
  }
}
