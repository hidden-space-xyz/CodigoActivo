import type { Gender } from '@/shared/api/generated/models'
import { i18n } from '@/shared/i18n'

export interface GenderOption {
  readonly label: string
  readonly value: Gender
}

const GENDERS: readonly Gender[] = ['Male', 'Female', 'Other']

export function genderLabel(gender: Gender): string {
  return i18n.global.t(`entities.user.gender.${gender}`)
}

export function genderOptions(): GenderOption[] {
  return GENDERS.map((value) => ({ label: genderLabel(value), value }))
}
