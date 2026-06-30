import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  changeUserTypeRequest,
  deleteUserRequest,
  getUserRequest,
  getUsersRequest,
  updateUserRequest,
  userQueryKeys,
} from '@/entities/user'
import type { UpdateUserRequest } from '@/shared/api/generated/models'

export function useUsers() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: userQueryKeys.all })

  const list = useQuery({
    queryKey: userQueryKeys.all,
    queryFn: ({ signal }) => getUsersRequest(signal),
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
    mutationFn: (vars: { id: string; roleId: string }) => changeUserTypeRequest(vars.id, vars.roleId),
    onSuccess: invalidate,
  })

  function fetchOne(id: string) {
    return getUserRequest(id)
  }

  return { list, update, remove, changeType, fetchOne }
}
