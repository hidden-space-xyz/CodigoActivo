import { mutationOptions } from '@tanstack/vue-query'

import { userKeys } from './queries'
import {
  changeUserTypeRequest,
  deleteUserRequest,
  resetUserTwoFactorRequest,
  setUserAdminRequest,
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
  /** Grants the admin role with the signed-in admin's password, or revokes it without one. */
  setAdmin: () =>
    mutationOptions({
      mutationFn: ({
        id,
        isAdmin,
        currentPassword,
      }: {
        id: string
        isAdmin: boolean
        currentPassword: string | null
      }) => setUserAdminRequest(id, isAdmin, currentPassword),
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
