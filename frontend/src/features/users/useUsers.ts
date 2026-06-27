import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiUsersUserId,
  getApiUsers,
  getApiUsersUserId,
  patchApiUsersUserIdChangeType,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import type { UpdateUserRequest } from '@/shared/api/generated/models'
import { queryKeys } from '@/shared/api/query-keys'

export function useUsers() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: queryKeys.users })

  const list = useQuery({
    queryKey: queryKeys.users,
    queryFn: ({ signal }) => getApiUsers({ signal }).then((r) => r.data ?? []),
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdateUserRequest }) =>
      putApiUsersUserId(vars.id, vars.body).then((r) => r.data),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteApiUsersUserId(id),
    onSuccess: invalidate,
  })

  const changeType = useMutation({
    mutationFn: (vars: { id: string; roleId: string }) =>
      patchApiUsersUserIdChangeType(vars.id, { roleId: vars.roleId }).then((r) => r.data),
    onSuccess: invalidate,
  })

  function fetchOne(id: string) {
    return getApiUsersUserId(id).then((r) => r.data)
  }

  return { list, update, remove, changeType, fetchOne }
}
