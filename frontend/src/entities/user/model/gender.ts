import type { TranslationKey } from '@/shared/i18n'

/** How a person identifies; the API refuses any other value. */
export type Gender = 'Male' | 'Female' | 'Other' | 'PreferNotToSay'

/** Every gender, in the order selectors list them. */
export const GENDERS: readonly Gender[] = ['Male', 'Female', 'Other', 'PreferNotToSay']

const GENDER_LABEL_KEYS: Record<Gender, TranslationKey> = {
  Male: 'entities.user.gender.Male',
  Female: 'entities.user.gender.Female',
  Other: 'entities.user.gender.Other',
  PreferNotToSay: 'entities.user.gender.PreferNotToSay',
}

/** Translation key of the label shown for a gender. */
export function genderLabelKey(gender: Gender): TranslationKey {
  return GENDER_LABEL_KEYS[gender]
}
