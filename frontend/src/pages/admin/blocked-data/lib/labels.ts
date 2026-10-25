import type { TranslationKey } from '@/shared/i18n'

import type { BlockedDataAuthority } from '../model/types'

/** Every authority a consultation can name, in the order the dialog lists them. */
export const AUTHORITIES: readonly BlockedDataAuthority[] = [
  'Court',
  'Prosecutor',
  'DataProtectionAuthority',
  'PublicAdministration',
  'Claim',
]

/** Translation key of the name of an authority. */
export function authorityLabelKey(authority: BlockedDataAuthority): TranslationKey {
  return `pages.admin.blockedData.authorities.${authority}` as TranslationKey
}

/** Translation key of the name of a kind of data, named after the entity of the history. */
export function entityLabelKey(entity: string): TranslationKey {
  return `pages.admin.blockedData.entities.${entity}` as TranslationKey
}
