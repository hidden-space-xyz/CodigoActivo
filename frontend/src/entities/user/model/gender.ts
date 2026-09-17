import type { Gender } from '@/shared/api/generated/models'
import { i18n, type TranslationKey } from '@/shared/i18n'

/** Select option pairing a translated label with the API `Gender` value. */
export interface GenderOption {
  readonly label: string
  readonly value: Gender
}

const GENDERS: readonly Gender[] = ['Male', 'Female', 'Other']
const GENDER_LABEL_KEYS: Record<Gender, TranslationKey> = {
  Male: 'entities.user.gender.Male',
  Female: 'entities.user.gender.Female',
  Other: 'entities.user.gender.Other',
}

/** Translates an API `Gender` value for display. */
export function genderLabel(gender: Gender): string {
  return i18n.global.t(GENDER_LABEL_KEYS[gender])
}

/** Builds translated select options for every gender, in a fixed order. */
export function genderOptions(): GenderOption[] {
  return GENDERS.map((value) => ({ label: genderLabel(value), value }))
}
