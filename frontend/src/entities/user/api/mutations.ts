import { mutationOptions } from '@tanstack/vue-query'

import type { StaffRole } from '../model/staff-role'
import { userKeys } from './queries'
import {
  changeUserTypeRequest,
  deleteUserRequest,
  designatePrivacyOfficerRequest,
  resetUserTwoFactorRequest,
  setUserStaffRoleRequest,
} from './requests'

/** Mutation options of users; each one refreshes every user query once it succeeds. */
export const userMutations = {
  remove: () =>
    mutationOptions({
      mutationFn: (id: string) => deleteUserRequest(id),
      meta: { invalidates: [userKeys.all] },
    }),
  changeType: () =>
    mutationOptions({
      mutationFn: ({ id, userTypeId }: { id: string; userTypeId: string }) =>
        changeUserTypeRequest(id, userTypeId),
      meta: { invalidates: [userKeys.all] },
    }),
  /** Gives a cargo with the superadministrator's password, or removes it without one. */
  setStaffRole: () =>
    mutationOptions({
      mutationFn: ({
        id,
        staffRole,
        currentPassword,
      }: {
        id: string
        staffRole: StaffRole
        currentPassword: string | null
      }) => setUserStaffRoleRequest(id, staffRole, currentPassword),
      meta: { invalidates: [userKeys.all] },
    }),
  /** Moves the data protection designation to a user with the superadministrator's password. */
  designatePrivacyOfficer: () =>
    mutationOptions({
      mutationFn: ({ id, currentPassword }: { id: string; currentPassword: string }) =>
        designatePrivacyOfficerRequest(id, currentPassword),
      meta: { invalidates: [userKeys.all] },
    }),
  /** Returns a user's second factor to email with the signed-in admin's password. */
  resetTwoFactor: () =>
    mutationOptions({
      mutationFn: ({ id, currentPassword }: { id: string; currentPassword: string }) =>
        resetUserTwoFactorRequest(id, currentPassword),
      meta: { invalidates: [userKeys.all] },
    }),
}
