import type { TranslationKey } from '@/shared/i18n'

/** Translation key of the name of a kind of data, named after the entity of the history. */
export function entityLabelKey(entity: string): TranslationKey {
  return `pages.admin.blockedData.entities.${entity}` as TranslationKey
}
