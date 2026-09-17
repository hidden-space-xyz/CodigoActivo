import { ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'

import {
  changeUserTypeRequest,
  deleteUserRequest,
  getUserRequest,
  getUsersPageRequest,
  setUserAdminRequest,
  updateUserRequest,
  userQueryKeys,
} from '@/entities/user'
import type { UpdateUserInput, User } from '@/entities/user'
import type { GetApiUsersParams } from '@/shared/api/generated/models'
import { fetchAllPages, useServerTable } from '@/shared/lib'

/** Narrows the users table to a relative of one user, such as their tutor or their dependents. */
export interface UserRelationFilter {
  /** Localized text shown in the active-filter chip. */
  readonly label: string
  /** Query parameters merged into every table request, e.g. `{ parentId }`. */
  readonly params: GetApiUsersParams
}

/**
 * Admin users table with column filters and an optional relation filter, plus update, delete,
 * user-type and admin-flag mutations that invalidate all user queries. Granting the admin flag
 * needs the signed-in admin's `currentPassword`. `fetchAllUsers` loads every page with the current
 * filters and sort (for exports).
 */
export function useUsers() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: userQueryKeys.all })

  const relationFilter = ref<UserRelationFilter | null>(null)

  const table = useServerTable<User, GetApiUsersParams>({
    queryKey: userQueryKeys.adminTable(),
    fetchPage: (params) => getUsersPageRequest(params),
    defaultSort: { field: 'firstName', order: 1 },
    columns: {
      name: { type: 'text' },
      email: { type: 'text' },
      phone: { type: 'text' },
      birthDate: { type: 'dateRange', fromParam: 'birthDateFrom', toParam: 'birthDateTo' },
      status: { param: 'userStatusTypeId' },
      type: { param: 'userTypeId' },
      isAdmin: { param: 'isAdmin' },
    },
    extraParams: () => ({ ...relationFilter.value?.params }),
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdateUserInput }) =>
      updateUserRequest(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteUserRequest(id),
    onSuccess: invalidate,
  })

  const changeType = useMutation({
    mutationFn: (vars: { id: string; userTypeId: string }) =>
      changeUserTypeRequest(vars.id, vars.userTypeId),
    onSuccess: invalidate,
  })

  const setAdmin = useMutation({
    mutationFn: (vars: { id: string; isAdmin: boolean; currentPassword?: string }) =>
      setUserAdminRequest(vars.id, vars.isAdmin, vars.currentPassword),
    onSuccess: invalidate,
  })

  function fetchOne(id: string) {
    return getUserRequest(id)
  }

  function fetchAllUsers(): Promise<User[]> {
    return fetchAllPages((params) => getUsersPageRequest(params as GetApiUsersParams), {
      ...table.filterParams.value,
      sort: table.sortParam.value,
    })
  }

  return {
    table,
    relationFilter,
    update,
    remove,
    changeType,
    setAdmin,
    fetchOne,
    fetchAllUsers,
  }
}
