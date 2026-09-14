import { createI18n } from 'vue-i18n'

import es from './locales/es.json'

type TranslationPath<T> = {
  [Key in keyof T & string]: T[Key] extends string
    ? Key
    : T[Key] extends Record<string, unknown>
      ? `${Key}.${TranslationPath<T[Key]>}`
      : never
}[keyof T & string]

export type TranslationKey = TranslationPath<typeof es>

export const i18n = createI18n({
  legacy: false,
  globalInjection: true,
  locale: 'es',
  fallbackLocale: 'es',
  messages: { es },
})
