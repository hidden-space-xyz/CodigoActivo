import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import { addAccountChild } from '@/modules/account/application/use-cases/add-account-child.use-case'
import { changeAccountPassword } from '@/modules/account/application/use-cases/change-account-password.use-case'
import { changeAccountRole } from '@/modules/account/application/use-cases/change-account-role.use-case'
import { changeChildRole as changeChildRoleUseCase } from '@/modules/account/application/use-cases/change-child-role.use-case'
import { deleteAccountChild } from '@/modules/account/application/use-cases/delete-account-child.use-case'
import { getAccountChildren } from '@/modules/account/application/use-cases/get-account-children.use-case'
import { getAccountProfile } from '@/modules/account/application/use-cases/get-account-profile.use-case'
import { getRegistrationTypes } from '@/modules/account/application/use-cases/get-registration-types.use-case'
import { updateAccountChild } from '@/modules/account/application/use-cases/update-account-child.use-case'
import { updateAccountProfile } from '@/modules/account/application/use-cases/update-account-profile.use-case'
import type { AccountProfile } from '@/modules/account/domain/entities/account-profile.entity'
import type {
  AddMinorInput,
  ChangePasswordInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '@/modules/account/domain/value-objects/account-inputs'
import { accountRepository } from '@/modules/account/infrastructure/repositories/account-repository.provider'
import { useAuth } from '@/modules/auth/presentation/composables/useAuth'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export function useAccount() {
  const session = useSessionStore()
  const { bootstrap } = useAuth()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)
  const profileKey = ['account', 'me'] as const
  const childrenKey = ['account', 'children'] as const

  const profile = useQuery({
    queryKey: profileKey,
    queryFn: () => getAccountProfile(accountRepository),
  })

  const children = useQuery({
    queryKey: childrenKey,
    queryFn: () => {
      if (!userId.value) return Promise.resolve([])
      return getAccountChildren(accountRepository, userId.value)
    },
    enabled: computed(() => userId.value !== null),
  })

  const adultRoles = useQuery({
    queryKey: ['registration-types', 'adult'],
    queryFn: () => getRegistrationTypes(accountRepository, false),
  })
  const minorRoles = useQuery({
    queryKey: ['registration-types', 'minor'],
    queryFn: () => getRegistrationTypes(accountRepository, true),
  })

  function invalidateChildren(): void {
    void queryClient.invalidateQueries({ queryKey: childrenKey })
  }

  function syncProfile(updated: AccountProfile): void {
    queryClient.setQueryData(profileKey, updated)
    void bootstrap()
  }

  const updateProfile = useMutation({
    mutationFn: (input: UpdateProfileInput) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return updateAccountProfile(accountRepository, userId.value, input)
    },
    onSuccess: syncProfile,
  })

  const changeOwnRole = useMutation({
    mutationFn: (roleId: string) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return changeAccountRole(accountRepository, userId.value, roleId)
    },
    onSuccess: syncProfile,
  })

  const changePassword = useMutation({
    mutationFn: (input: ChangePasswordInput) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return changeAccountPassword(accountRepository, userId.value, input)
    },
  })

  const addChild = useMutation({
    mutationFn: (input: AddMinorInput) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return addAccountChild(accountRepository, userId.value, input)
    },
    onSuccess: invalidateChildren,
  })

  const updateChild = useMutation({
    mutationFn: (vars: { childId: string; input: UpdateMinorInput }) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return updateAccountChild(accountRepository, vars.childId, userId.value, vars.input)
    },
    onSuccess: invalidateChildren,
  })

  const changeChildRole = useMutation({
    mutationFn: (vars: { childId: string; roleId: string }) =>
      changeChildRoleUseCase(accountRepository, vars.childId, vars.roleId),
    onSuccess: invalidateChildren,
  })

  const deleteChild = useMutation({
    mutationFn: (childId: string) => deleteAccountChild(accountRepository, childId),
    onSuccess: invalidateChildren,
  })

  return {
    userId,
    profile,
    children,
    adultRoles,
    minorRoles,
    updateProfile,
    changeOwnRole,
    changePassword,
    addChild,
    updateChild,
    changeChildRole,
    deleteChild,
  }
}
