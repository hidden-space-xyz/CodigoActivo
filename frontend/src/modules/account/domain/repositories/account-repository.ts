import type { AccountChild } from '@/modules/account/domain/entities/account-child.entity'
import type { AccountProfile } from '@/modules/account/domain/entities/account-profile.entity'
import type { RegistrationType } from '@/modules/account/domain/entities/registration-type.entity'
import type {
  AddMinorInput,
  ChangePasswordInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '@/modules/account/domain/value-objects/account-inputs'

export interface AccountRepository {
  getProfile(): Promise<AccountProfile | null>
  getChildren(parentId: string): Promise<readonly AccountChild[]>
  getRegistrationTypes(minor: boolean): Promise<readonly RegistrationType[]>
  updateProfile(userId: string, input: UpdateProfileInput): Promise<AccountProfile>
  changeRole(userId: string, roleId: string): Promise<AccountProfile>
  changePassword(userId: string, input: ChangePasswordInput): Promise<void>
  addChild(parentId: string, input: AddMinorInput): Promise<AccountChild>
  updateChild(childId: string, parentId: string, input: UpdateMinorInput): Promise<AccountChild>
  changeChildRole(childId: string, roleId: string): Promise<AccountChild>
  deleteChild(childId: string): Promise<void>
}
