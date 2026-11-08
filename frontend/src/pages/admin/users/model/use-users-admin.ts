import { computed, readonly, ref, type Ref } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { refreshSession, useSession } from '@/entities/session'
import {
  fullName,
  userList,
  userMutations,
  userQueries,
  type StaffRole,
  type User,
} from '@/entities/user'
import { hasErrorCode } from '@/shared/api'
import { useCrudDialog } from '@/shared/lib/crud'
import { getErrorMessage, useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'
import type { TableFilterChip } from '@/shared/ui/data-table'

type UserListScope = 'independent' | 'dependent' | 'all'

const SCOPE_PARAMS: Record<UserListScope, Readonly<Record<string, string>>> = {
  independent: { scope: 'Independent' },
  dependent: { scope: 'Dependent' },
  all: {},
}

const DEFAULT_SORT_FIELD = 'firstName'

const HIDDEN_FIELDS: Record<UserListScope, readonly string[]> = {
  independent: ['birthDate'],
  dependent: ['email', 'phone', 'nationalId', 'status', 'staffRole', 'newsConsent'],
  all: [],
}

/** Narrows the user table to a relative of one user: their guardian or their dependents. */
export interface UserRelationFilter {
  /** Localized text shown in the active-filter chip. */
  readonly label: string
  /** Query parameters merged into every request: `id` of a guardian or `parentId` of dependents. */
  readonly params: Readonly<Record<string, string>>
}

/**
 * Admin user list: a table of the independent accounts, with a scope switch to the dependents or
 * everyone, a filter per column and an optional relation filter (also listed as a table chip) that
 * moves to the scope of the people it shows and is dropped when the scope changes. Each scope hides
 * the fields its people never have (`showsField`) and drops their filters and their sort: the
 * birth date of independent accounts, and the contact details, DNI/NIE, status, staff role and news
 * consent of dependents.
 *
 * It also opens the dialogs that change a user's type or return their second factor to email, and,
 * for the superadministrator alone, those that change the staff role of another independent
 * account or designate the data protection officer; personal data and consents are only changed by
 * their holder or guardian from «Mi cuenta». Resetting the second factor, designating the officer
 * and giving any staff role but none need the caller's password: a refused one is shown in the
 * dialog instead of a toast. Only the superadministrator deletes, after confirmation; they can
 * never be deleted, and the caller can neither change their own type nor delete their own account
 * here; the block reasons explain why. Designating the officer away from or onto the caller
 * reloads their session. Call it in `setup`.
 */
export function useUsersAdmin() {
  const { t } = useI18n()
  const session = useSession()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

  const scope = ref<UserListScope>('independent')
  const relationFilter = ref<UserRelationFilter | null>(null)
  const table = useServerTable({
    ...userList,
    defaultSort: { field: DEFAULT_SORT_FIELD, order: 1 },
    columns: {
      name: { type: 'text' },
      email: { type: 'text' },
      phone: { type: 'text' },
      nationalId: { type: 'text' },
      birthDate: { type: 'dateRange', fromParam: 'birthDateFrom', toParam: 'birthDateTo' },
      status: { param: 'userStatusTypeId' },
      type: { param: 'userTypeId' },
      staffRole: { param: 'staffRole' },
      newsConsent: { param: 'newsConsent' },
    },
    extraParams: () => ({ ...SCOPE_PARAMS[scope.value], ...relationFilter.value?.params }),
  })
  const types = useQuery(userQueries.types())
  const statuses = useQuery(userQueries.statuses())

  const remove = useMutation(userMutations.remove())
  const changeType = useMutation(userMutations.changeType())
  const setStaffRole = useMutation(userMutations.setStaffRole())
  const designateOfficer = useMutation(userMutations.designatePrivacyOfficer())
  const resetTwoFactor = useMutation(userMutations.resetTwoFactor())

  const typeDialog = useCrudDialog<User>()
  const staffRoleDialog = useCrudDialog<User>()
  const officerDialog = useCrudDialog<User>()
  const resetDialog = useCrudDialog<User>()
  const staffRoleError = ref('')
  const officerError = ref('')
  const resetError = ref('')

  function passwordAware(error: Ref<string>, successMessage: string, done: () => void) {
    return {
      onSuccess: () => {
        feedback.success(successMessage)
        done()
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

  function showsField(field: string): boolean {
    return !HIDDEN_FIELDS[scope.value].includes(field)
  }

  function changeScope(next: UserListScope): void {
    scope.value = next
    const hidden = HIDDEN_FIELDS[next]
    for (const field of hidden) table.columnFilter(field).value = null
    if (hidden.includes(table.sortField.value ?? '')) table.setSort(DEFAULT_SORT_FIELD)
  }

  function showGuardianOf(user: User): void {
    if (!user.parentId) return
    table.clearFilters()
    changeScope('independent')
    relationFilter.value = {
      label: t('pages.admin.users.relation.tutorOf', { fullName: fullName(user) }),
      params: { id: user.parentId },
    }
  }

  function showDependentsOf(user: User): void {
    table.clearFilters()
    changeScope('dependent')
    relationFilter.value = {
      label: t('pages.admin.users.relation.dependentsOf', { fullName: fullName(user) }),
      params: { parentId: user.id },
    }
  }

  function clearRelationFilter(): void {
    relationFilter.value = null
  }

  function setScope(next: UserListScope): void {
    changeScope(next)
    clearRelationFilter()
  }

  const relationChips = computed<TableFilterChip[]>(() =>
    relationFilter.value
      ? [{ key: 'relation', label: relationFilter.value.label, remove: clearRelationFilter }]
      : [],
  )

  function isOwnAccount(user: User): boolean {
    const own = session.user
    return own !== null && own.id === user.id
  }

  function typeBlockReason(user: User): string | undefined {
    return isOwnAccount(user) ? t('pages.admin.users.blocked.selfRole') : undefined
  }

  function mayChangeStaffRole(user: User): boolean {
    return session.isSuperAdmin && !user.parentId && !isOwnAccount(user)
  }

  function mayDesignatePrivacyOfficer(user: User): boolean {
    return session.isSuperAdmin && user.staffRole === 'Administrator' && !user.isPrivacyOfficer
  }

  const mayDelete = computed(() => session.isSuperAdmin)

  function deleteBlockReason(user: User): string | undefined {
    if (user.isInitialAdmin) return t('pages.admin.users.blocked.initialAdmin')
    if (isOwnAccount(user)) return t('pages.admin.users.blocked.self')
    return undefined
  }

  function saveType(userTypeId: string): void {
    const user = typeDialog.editing.value
    if (!user) return
    changeType.mutate(
      { id: user.id, userTypeId },
      feedback.outcome(t('pages.admin.users.toasts.typeUpdated'), typeDialog.close),
    )
  }

  function openStaffRole(user: User): void {
    staffRoleError.value = ''
    void staffRoleDialog.openEdit(user)
  }

  function saveStaffRole(staffRole: StaffRole, currentPassword: string | null): void {
    const user = staffRoleDialog.editing.value
    if (!user) return
    staffRoleError.value = ''
    setStaffRole.mutate(
      { id: user.id, staffRole, currentPassword },
      passwordAware(
        staffRoleError,
        t('pages.admin.users.toasts.staffRoleChanged'),
        staffRoleDialog.close,
      ),
    )
  }

  function openPrivacyOfficer(user: User): void {
    officerError.value = ''
    void officerDialog.openEdit(user)
  }

  function designatePrivacyOfficer(currentPassword: string): void {
    const user = officerDialog.editing.value
    if (!user) return
    officerError.value = ''
    const changesSession = isOwnAccount(user) || session.isPrivacyOfficer
    designateOfficer.mutate(
      { id: user.id, currentPassword },
      passwordAware(officerError, t('pages.admin.users.toasts.privacyOfficerDesignated'), () => {
        officerDialog.close()
        if (changesSession) void refreshSession(queryClient)
      }),
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
    scope: readonly(scope),
    setScope,
    showsField,
    relationChips,
    showGuardianOf,
    showDependentsOf,
    isOwnAccount,
    typeBlockReason,
    mayChangeStaffRole,
    mayDesignatePrivacyOfficer,
    mayDelete,
    deleteBlockReason,
    typeDialog,
    saveType,
    changingType: changeType.isPending,
    staffRoleDialog,
    staffRoleError,
    openStaffRole,
    saveStaffRole,
    settingStaffRole: setStaffRole.isPending,
    officerDialog,
    officerError,
    openPrivacyOfficer,
    designatePrivacyOfficer,
    designatingOfficer: designateOfficer.isPending,
    resetDialog,
    resetError,
    openResetTwoFactor,
    resetUserTwoFactor,
    resettingTwoFactor: resetTwoFactor.isPending,
    confirmRemove,
  }
}
