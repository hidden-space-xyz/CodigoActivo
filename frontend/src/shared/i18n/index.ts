import { createI18n } from 'vue-i18n'

import { es } from './locales/es'

export const i18n = createI18n({
  legacy: false,
  globalInjection: true,
  locale: 'es',
  fallbackLocale: 'es',
  messages: { es },
})
