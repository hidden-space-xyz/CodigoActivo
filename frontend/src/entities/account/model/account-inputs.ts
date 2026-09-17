import type { Gender } from '@/shared/api/generated/models'

/** Values from the profile form for editing the signed-in adult's own data. */
export interface UpdateProfileInput {
  firstName: string
  lastName: string
  email: string
  phone: string
  birthDate: string
  gender: Gender
}

/** Password change form values; confirmation is checked by the form before this is built. */
export interface ChangePasswordInput {
  currentPassword: string
  newPassword: string
}

/** Values needed to drop the authenticator application and receive login codes by email again. */
export interface DisableAuthenticatorInput {
  /** Current password, re-entered so a stolen session cannot weaken the second factor. */
  currentPassword: string
  /** Current code of the authenticator being removed. */
  code: string
}

/** Form values for registering a minor in the household; minors have no email or phone. */
export interface AddMinorInput {
  firstName: string
  lastName: string
  birthDate: string
  gender: Gender
}

/** Form values for editing an existing minor; the parent link is supplied separately. */
export interface UpdateMinorInput {
  firstName: string
  lastName: string
  birthDate: string
  gender: Gender
}

/** Rating form values for an attended event; empty comments are sent as `null`. */
export interface EventRatingInput {
  /** Stars from 0 to 5; `0` means the user cleared the score. */
  score: number
  mostLiked: string
  leastLiked: string
  suggestions: string
}
