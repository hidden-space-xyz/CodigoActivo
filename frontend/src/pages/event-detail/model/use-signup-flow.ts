import { computed, reactive, ref } from 'vue'
import { useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import type {
  ActivityOverlap,
  HouseholdAssignmentInput,
  TermsDecisionInput,
} from '@/entities/activity'
import { eventKeys } from '@/entities/event'
import { hasErrorCode } from '@/shared/api'
import { useActionConfirm, useCrudFeedback } from '@/shared/lib/feedback'

import type { TimelineActivity } from './types'
import type { useEventActivities } from './use-event-activities'

/** A signup waiting for the user's decisions on the event's terms documents. */
type PendingSignup =
  | { readonly kind: 'self'; readonly activityId: string; readonly roleId: string }
  | {
      readonly kind: 'household'
      readonly activityId: string
      readonly assignments: readonly HouseholdAssignmentInput[]
    }

/** A household member offered in the household signup dialog. */
interface HouseholdRow {
  userId: string
  name: string
  alreadyAssigned: boolean
  assignedRole: string
  include: boolean
  roleId: string
}

/** What the signup flow of an event page works with. */
interface SignupFlowOptions {
  readonly eventId: () => string
  /** Whether the user may sign up right now. */
  readonly signupOpen: () => boolean
  /** Whether the event has terms documents the user may have to decide on. */
  readonly hasTerms: () => boolean
  readonly activities: ReturnType<typeof useEventActivities>
}

/**
 * Signup flow of an event's activities. Signing oneself up first checks for overlapping
 * activities and asks for confirmation when there are some; signing the household up asks for a
 * role per member. Either way, pending terms documents are offered before sending, and a signup
 * the API refuses for missing terms reopens them. Withdrawals ask for confirmation first, and
 * activities that already started accept neither. Only one activity is busy at a time. Call it in
 * `setup`.
 */
export function useSignupFlow(options: SignupFlowOptions) {
  const { t } = useI18n()
  const router = useRouter()
  const feedback = useCrudFeedback()
  const { confirmAction } = useActionConfirm()
  const queryClient = useQueryClient()
  const { assign, assignHousehold, unassign, verifyOverlaps, termsState, members, rolesFor } =
    options.activities

  const busyId = ref<string | null>(null)
  const overlapDialog = reactive<{
    visible: boolean
    activity: TimelineActivity | null
    roleId: string
    overlaps: readonly ActivityOverlap[]
  }>({ visible: false, activity: null, roleId: '', overlaps: [] })
  const householdDialog = reactive<{
    visible: boolean
    activity: TimelineActivity | null
    rows: HouseholdRow[]
  }>({ visible: false, activity: null, rows: [] })
  const termsDialog = reactive<{ visible: boolean; action: PendingSignup | null }>({
    visible: false,
    action: null,
  })

  const pendingTermsDocuments = computed(() =>
    (termsState.data.value?.documents ?? []).filter((document) => document.accepted !== true),
  )
  const hasPendingTerms = computed(() => pendingTermsDocuments.value.length > 0)
  const householdSelectable = computed(() =>
    householdDialog.rows.filter((row) => !row.alreadyAssigned),
  )
  const householdHighDemand = computed(() => {
    const saturated = householdDialog.activity?.highDemandRoleIds ?? []
    return householdDialog.rows.some(
      (row) =>
        row.include && !row.alreadyAssigned && !!row.roleId && saturated.includes(row.roleId),
    )
  })

  function askTerms(action: PendingSignup): void {
    termsDialog.action = action
    termsDialog.visible = true
  }

  function handleSignupError(error: unknown, action: PendingSignup): void {
    if (hasErrorCode(error, 'EventTermsAcceptanceRequired')) {
      void queryClient.invalidateQueries({ queryKey: eventKeys.detail(options.eventId()) })
      void queryClient.invalidateQueries({ queryKey: eventKeys.terms(options.eventId()) })
      if (options.hasTerms()) {
        askTerms(action)
        return
      }
    }
    feedback.error(error, t('pages.eventDetail.toast.signupFailed'))
  }

  function goLogin(): void {
    const redirect = router.resolve({
      name: 'event-detail',
      params: { eventId: options.eventId() },
    }).fullPath
    void router.push({ name: 'login', query: { redirect } })
  }

  function signUp(
    activityId: string,
    roleId: string,
    termsDecisions?: readonly TermsDecisionInput[],
  ): void {
    busyId.value = activityId
    assign.mutate(
      { activityId, roleId, termsDecisions },
      {
        onSuccess: () => {
          feedback.success(
            t('pages.eventDetail.toast.signupSuccess'),
            t('pages.eventDetail.toast.signupSent'),
          )
        },
        onError: (error) => {
          handleSignupError(error, { kind: 'self', activityId, roleId })
        },
        onSettled: () => {
          busyId.value = null
        },
      },
    )
  }

  function signUpHousehold(
    activityId: string,
    assignments: readonly HouseholdAssignmentInput[],
    termsDecisions?: readonly TermsDecisionInput[],
  ): void {
    busyId.value = activityId
    assignHousehold.mutate(
      { activityId, assignments, termsDecisions },
      {
        onSuccess: () => {
          householdDialog.visible = false
          feedback.success(
            t('pages.eventDetail.toast.householdSuccess', assignments.length),
            t('pages.eventDetail.toast.signupSent'),
          )
        },
        onError: (error) => {
          handleSignupError(error, { kind: 'household', activityId, assignments })
        },
        onSettled: () => {
          busyId.value = null
        },
      },
    )
  }

  function signUpOrAskTerms(activityId: string, roleId: string): void {
    if (hasPendingTerms.value) {
      askTerms({ kind: 'self', activityId, roleId })
      busyId.value = null
      return
    }
    signUp(activityId, roleId)
  }

  async function onSignup(activity: TimelineActivity, roleId: string): Promise<void> {
    if (!options.signupOpen() || activity.started) return
    busyId.value = activity.id
    try {
      const overlap = await verifyOverlaps(activity.id)
      if (overlap?.hasOverlaps) {
        overlapDialog.activity = activity
        overlapDialog.roleId = roleId
        overlapDialog.overlaps = overlap.overlaps
        overlapDialog.visible = true
        busyId.value = null
        return
      }
      signUpOrAskTerms(activity.id, roleId)
    } catch (error) {
      busyId.value = null
      feedback.error(error)
    }
  }

  function confirmOverlapSignup(): void {
    if (!overlapDialog.activity) return
    signUpOrAskTerms(overlapDialog.activity.id, overlapDialog.roleId)
    overlapDialog.visible = false
  }

  function confirmTerms(decisions: TermsDecisionInput[]): void {
    const action = termsDialog.action
    termsDialog.visible = false
    termsDialog.action = null
    if (!action) return
    if (action.kind === 'self') signUp(action.activityId, action.roleId, decisions)
    else signUpHousehold(action.activityId, action.assignments, decisions)
  }

  function openHousehold(activity: TimelineActivity): void {
    householdDialog.activity = activity
    householdDialog.rows = members.value.map((member) => {
      const existing = activity.household.find((assigned) => assigned.userId === member.id)
      const memberRoles = rolesFor(member.id)
      return {
        userId: member.id,
        name: member.name,
        alreadyAssigned: existing !== undefined,
        assignedRole: existing?.roleName ?? '',
        include: existing === undefined,
        roleId: memberRoles.length === 1 ? (memberRoles[0]?.id ?? '') : '',
      }
    })
    householdDialog.visible = true
  }

  function confirmHousehold(): void {
    const activity = householdDialog.activity
    if (!activity) return
    const included = householdDialog.rows.filter((row) => row.include && !row.alreadyAssigned)
    if (included.some((row) => !row.roleId)) {
      feedback.warn(
        t('pages.eventDetail.toast.missingRoleDetail'),
        t('pages.eventDetail.toast.missingRole'),
      )
      return
    }
    const assignments = included.map((row) => ({ userId: row.userId, roleId: row.roleId }))
    if (assignments.length === 0) {
      householdDialog.visible = false
      return
    }
    if (hasPendingTerms.value) {
      askTerms({ kind: 'household', activityId: activity.id, assignments })
      return
    }
    signUpHousehold(activity.id, assignments)
  }

  function withdraw(activity: TimelineActivity, memberId: string): void {
    busyId.value = activity.id
    unassign.mutate(
      { activityId: activity.id, userId: memberId },
      {
        onSuccess: () => {
          feedback.success(
            t('pages.eventDetail.toast.unassignSuccess'),
            t('pages.eventDetail.toast.unassignSummary'),
          )
        },
        onError: (error) => {
          feedback.error(error)
        },
        onSettled: () => {
          busyId.value = null
        },
      },
    )
  }

  function onUnassignMember(activity: TimelineActivity, memberId: string): void {
    if (!options.signupOpen() || activity.started) return
    const member = activity.household.find((assigned) => assigned.userId === memberId)
    const message =
      member && memberId !== options.activities.userId.value
        ? t('pages.eventDetail.unassignConfirm.member', {
            name: member.name,
            activity: activity.title,
          })
        : t('pages.eventDetail.unassignConfirm.self', { activity: activity.title })
    confirmAction({
      header: t('pages.eventDetail.unassignConfirm.header'),
      message,
      acceptLabel: t('pages.eventDetail.unassignConfirm.accept'),
      accept: () => {
        withdraw(activity, memberId)
      },
    })
  }

  function onUnassign(activity: TimelineActivity): void {
    const userId = options.activities.userId.value
    if (userId) onUnassignMember(activity, userId)
  }

  return {
    busyId,
    overlapDialog,
    householdDialog,
    termsDialog,
    pendingTermsDocuments,
    householdSelectable,
    householdHighDemand,
    onSignup,
    confirmOverlapSignup,
    confirmTerms,
    openHousehold,
    confirmHousehold,
    onUnassign,
    onUnassignMember,
    goLogin,
  }
}
