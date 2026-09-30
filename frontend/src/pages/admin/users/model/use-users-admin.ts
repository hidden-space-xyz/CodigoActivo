import { ref, type Ref } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  fullName,
  userList,
  userMutations,
  userQueries,
  type UpdateUserInput,
  type User,
} from '@/entities/user'
import { hasErrorCode } from '@/shared/api'
import { useCrudDialog } from '@/shared/lib/crud'
import { getErrorMessage, useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

/** Narrows the user table to a relative of one user: their guardian or their dependents. */
export interface UserRelationFilter {
  /** Localized text shown in the active-filter chip. */
  readonly label: string
  /** Query parameters merged into every table request, e.g. `{ parentId }`. */
  readonly params: Readonly<Record<string, string>>
}

/**
 * Admin user list: a table with a filter per column and an optional relation filter, and the
 * dialogs that edit a user (loaded whole first), change their type, grant the admin role or
 * return their second factor to email. The last two, and any edit that replaces contact details,
 * need the signed-in admin's password: a refused one is shown in the dialog instead of a toast.
 * Revoking the admin role and deleting (after confirmation) act at once. Call it in `setup`.
 */
export function useUsersAdmin() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

  const relationFilter = ref<UserRelationFilter | null>(null)
  const table = useServerTable({
    ...userList,
    defaultSort: { field: 'firstName', order: 1 },
    columns: {
      name: { type: 'text' },
      email: { type: 'text' },
      phone: { type: 'text' },
      nationalId: { type: 'text' },
      birthDate: { type: 'dateRange', fromParam: 'birthDateFrom', toParam: 'birthDateTo' },
      status: { param: 'userStatusTypeId' },
      type: { param: 'userTypeId' },
      isAdmin: { param: 'isAdmin' },
      promotionalConsent: { param: 'promotionalConsent' },
    },
    extraParams: () => ({ ...relationFilter.value?.params }),
  })
  const types = useQuery(userQueries.types())
  const statuses = useQuery(userQueries.statuses())

  const update = useMutation(userMutations.update())
  const remove = useMutation(userMutations.remove())
  const changeType = useMutation(userMutations.changeType())
  const setAdmin = useMutation(userMutations.setAdmin())
  const resetTwoFactor = useMutation(userMutations.resetTwoFactor())

  const editDialog = useCrudDialog<User>({
    load: (row) =>
      queryClient.fetchQuery({ ...userQueries.detail(row.id), staleTime: 0 }).catch(() => row),
  })
  const typeDialog = useCrudDialog<User>()
  const grantDialog = useCrudDialog<User>()
  const resetDialog = useCrudDialog<User>()
  const editError = ref('')
  const grantError = ref('')
  const resetError = ref('')

  function passwordAware(error: Ref<string>, successMessage: string, close: () => void) {
    return {
      onSuccess: () => {
        feedback.success(successMessage)
        close()
      },
      onError: (failure: unknown) => {
        if (hasErrorCode(failure, 'UserCurrentPasswordIncorrect')) {
          error.value = getErrorMessage(failure)
          return
        }
        feedback.error(failure)
      },
    }
  }

  function showGuardianOf(user: User): void {
    if (!user.parentId) return
    table.clearFilters()
    relationFilter.value = {
      label: t('pages.admin.users.relation.tutorOf', { fullName: fullName(user) }),
      params: { id: user.parentId },
    }
  }

  function showDependentsOf(user: User): void {
    table.clearFilters()
    relationFilter.value = {
      label: t('pages.admin.users.relation.dependentsOf', { fullName: fullName(user) }),
      params: { parentId: user.id },
    }
  }

  function clearRelationFilter(): void {
    relationFilter.value = null
  }

  function openEdit(user: User): Promise<void> {
    editError.value = ''
    return editDialog.openEdit(user)
  }

  function saveUser(input: UpdateUserInput): void {
    const user = editDialog.editing.value
    if (!user) return
    editError.value = ''
    update.mutate(
      { id: user.id, input },
      passwordAware(editError, t('pages.admin.users.toasts.updated'), editDialog.close),
    )
  }

  function saveType(userTypeId: string): void {
    const user = typeDialog.editing.value
    if (!user) return
    changeType.mutate(
      { id: user.id, userTypeId },
      feedback.outcome(t('pages.admin.users.toasts.typeUpdated'), typeDialog.close),
    )
  }

  function toggleAdmin(user: User, isAdmin: boolean): void {
    if (isAdmin) {
      grantError.value = ''
      void grantDialog.openEdit(user)
      return
    }
    setAdmin.mutate(
      { id: user.id, isAdmin: false, currentPassword: null },
      feedback.outcome(t('pages.admin.users.toasts.adminRevoked')),
    )
  }

  function grantAdmin(currentPassword: string): void {
    const user = grantDialog.editing.value
    if (!user) return
    grantError.value = ''
    setAdmin.mutate(
      { id: user.id, isAdmin: true, currentPassword },
      passwordAware(grantError, t('pages.admin.users.toasts.adminGranted'), grantDialog.close),
    )
  }

  function openResetTwoFactor(user: User): void {
    resetError.value = ''
    void resetDialog.openEdit(user)
  }

  function resetUserTwoFactor(currentPassword: string): void {
    const user = resetDialog.editing.value
    if (!user) return
    resetError.value = ''
    resetTwoFactor.mutate(
      { id: user.id, currentPassword },
      passwordAware(resetError, t('pages.admin.users.toasts.twoFactorReset'), resetDialog.close),
    )
  }

  function confirmRemove(user: User): void {
    confirmDelete({
      header: t('pages.admin.users.delete.header'),
      message: t('pages.admin.users.delete.message', { fullName: fullName(user) }),
      accept: () => {
        remove.mutate(user.id, feedback.outcome(t('pages.admin.users.toasts.deleted')))
      },
    })
  }

  return {
    table,
    types,
    statuses,
    relationFilter,
    showGuardianOf,
    showDependentsOf,
    clearRelationFilter,
    editDialog,
    editError,
    openEdit,
    saveUser,
    saving: update.isPending,
    typeDialog,
    saveType,
    changingType: changeType.isPending,
    grantDialog,
    grantError,
    toggleAdmin,
    grantAdmin,
    settingAdmin: setAdmin.isPending,
    resetDialog,
    resetError,
    openResetTwoFactor,
    resetUserTwoFactor,
    resettingTwoFactor: resetTwoFactor.isPending,
    confirmRemove,
  }
}
