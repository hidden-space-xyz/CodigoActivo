export interface UpdateProfileInput {
  firstName: string
  lastName: string
  email: string
  phone: string
  birthDate: string
}

export interface ChangePasswordInput {
  currentPassword: string
  newPassword: string
}

export interface AddMinorInput {
  firstName: string
  lastName: string
  birthDate: string
  roleId: string
}

export interface UpdateMinorInput {
  firstName: string
  lastName: string
  birthDate: string
}
