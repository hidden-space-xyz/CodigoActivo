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
import type {
  GetApiUsersParams,
  UpdateUserRequest,
  UserResponse,
} from '@/shared/api/generated/models'
import { useServerTable } from '@/shared/lib'

export interface UserRelationFilter {
  readonly label: string
  readonly params: GetApiUsersParams
}

export function useUsers() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: userQueryKeys.all })

  const relationFilter = ref<UserRelationFilter | null>(null)

  const table = useServerTable<UserResponse, GetApiUsersParams>({
    queryKey: [...userQueryKeys.all, 'table'],
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
    mutationFn: (vars: { id: string; body: UpdateUserRequest }) =>
      updateUserRequest(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteUserRequest(id),
    onSuccess: invalidate,
  })

  const changeType = useMutation({
    mutationFn: (vars: { id: string; roleId: string }) =>
      changeUserTypeRequest(vars.id, vars.roleId),
    onSuccess: invalidate,
  })

  const setAdmin = useMutation({
    mutationFn: (vars: { id: string; isAdmin: boolean }) =>
      setUserAdminRequest(vars.id, vars.isAdmin),
    onSuccess: invalidate,
  })

  function fetchOne(id: string) {
    return getUserRequest(id)
  }

  return { table, relationFilter, update, remove, changeType, setAdmin, fetchOne }
}
