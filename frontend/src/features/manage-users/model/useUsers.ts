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

const USERS_EXPORT_PAGE_SIZE = 100
const USERS_EXPORT_PAGE_LIMIT = 200

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
    mutationFn: (vars: { id: string; userTypeId: string }) =>
      changeUserTypeRequest(vars.id, vars.userTypeId),
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

  async function fetchAllUsers(): Promise<UserResponse[]> {
    const filters = table.filterParams.value
    const sort = table.sortParam.value
    const collected: UserResponse[] = []

    for (let page = 1; page <= USERS_EXPORT_PAGE_LIMIT; page += 1) {
      const params = {
        ...filters,
        sort,
        page,
        pageSize: USERS_EXPORT_PAGE_SIZE,
      } as GetApiUsersParams
      const { items, total } = await getUsersPageRequest(params)
      collected.push(...items)
      if (items.length === 0 || collected.length >= total) break
    }

    return collected
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
