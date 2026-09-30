import type { TranslationKey } from '@/shared/i18n'

/** A cookie or Web Storage key the site writes, with what it is for and how long it lasts. */
export interface StorageEntry {
  readonly name: string
  readonly kindKey: TranslationKey
  readonly purposeKey: TranslationKey
  readonly durationKey: TranslationKey
}

/** Help page of a browser explaining how to delete cookies. */
export interface BrowserGuide {
  readonly labelKey: TranslationKey
  readonly url: string
}

/** Every cookie and Web Storage key the site writes; cookie names are the production ones. */
export const STORAGE_ENTRIES: readonly StorageEntry[] = [
  {
    name: '__Host-CodigoActivo.Session',
    kindKey: 'pages.cookiePolicy.used.kinds.cookie',
    purposeKey: 'pages.cookiePolicy.used.session.purpose',
    durationKey: 'pages.cookiePolicy.used.session.duration',
  },
  {
    name: '__Host-CodigoActivo.TwoFactor',
    kindKey: 'pages.cookiePolicy.used.kinds.cookie',
    purposeKey: 'pages.cookiePolicy.used.twoFactor.purpose',
    durationKey: 'pages.cookiePolicy.used.twoFactor.duration',
  },
  {
    name: '__Host-CodigoActivo.Csrf',
    kindKey: 'pages.cookiePolicy.used.kinds.cookie',
    purposeKey: 'pages.cookiePolicy.used.csrf.purpose',
    durationKey: 'pages.cookiePolicy.used.csrf.duration',
  },
  {
    name: 'ca-theme',
    kindKey: 'pages.cookiePolicy.used.kinds.localStorage',
    purposeKey: 'pages.cookiePolicy.used.theme.purpose',
    durationKey: 'pages.cookiePolicy.used.theme.duration',
  },
  {
    name: 'ca:stale-build-reload',
    kindKey: 'pages.cookiePolicy.used.kinds.sessionStorage',
    purposeKey: 'pages.cookiePolicy.used.staleBuild.purpose',
    durationKey: 'pages.cookiePolicy.used.staleBuild.duration',
  },
]

/** Browser help pages for deleting cookies. */
export const BROWSER_GUIDES: readonly BrowserGuide[] = [
  {
    labelKey: 'pages.cookiePolicy.manage.chrome',
    url: 'https://support.google.com/chrome/answer/95647?hl=es',
  },
  {
    labelKey: 'pages.cookiePolicy.manage.firefox',
    url: 'https://support.mozilla.org/es/kb/Borrar%20cookies',
  },
  {
    labelKey: 'pages.cookiePolicy.manage.safariIos',
    url: 'https://support.apple.com/es-es/105082',
  },
  {
    labelKey: 'pages.cookiePolicy.manage.safariMac',
    url: 'https://support.apple.com/es-es/guide/safari/sfri11471/mac',
  },
  {
    labelKey: 'pages.cookiePolicy.manage.edge',
    url: 'https://support.microsoft.com/es-es/microsoft-edge/eliminar-las-cookies-en-microsoft-edge-63947406-40ac-c3b8-57b9-2a946a29ae09',
  },
]

/** Spanish data protection authority. */
export const AUTHORITY_URL = 'https://www.aepd.es'

/** Guide of the Spanish data protection authority on the use of cookies. */
export const COOKIE_GUIDE_URL = 'https://www.aepd.es/guias/guia-cookies.pdf'
