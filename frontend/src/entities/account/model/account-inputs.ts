import type { Gender } from '@/shared/api/generated/models'

export interface UpdateProfileInput {
  firstName: string
  lastName: string
  email: string
  phone: string
  birthDate: string
  gender: Gender
}

export interface ChangePasswordInput {
  currentPassword: string
  newPassword: string
}

export interface AddMinorInput {
  firstName: string
  lastName: string
  birthDate: string
  gender: Gender
}

export interface UpdateMinorInput {
  firstName: string
  lastName: string
  birthDate: string
  gender: Gender
}

export interface EventRatingInput {
  score: number
  mostLiked: string
  leastLiked: string
  suggestions: string
}
