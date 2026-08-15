<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useQueryClient } from '@tanstack/vue-query'
import { AppButton as Button, AppIcon, RichTextContent } from '@/shared/ui'

import { useEventActivities } from '@/features/activity-signup'
import ActivityTimelineCard from './ActivityTimelineCard.vue'
import type { TimelineActivity, TimelineMemberAssignment } from '../model/types'
import type { ActivityOverlap, HouseholdAssignmentInput } from '@/entities/activity'
import { eventQueryKeys } from '@/entities/event'
import type { EventTermsInfo } from '@/entities/event'
import { ApiError } from '@/shared/api'
import { formatDateTime, formatDateTimeRange, useCrudFeedback } from '@/shared/lib'

const props = defineProps<{
  eventId: string
  signupOpen: boolean
  earlyOnly?: boolean
  terms?: EventTermsInfo | null
}>()

const router = useRouter()
const { t } = useI18n()
const feedback = useCrudFeedback()
const queryClient = useQueryClient()
const {
  activities,
  assigned,
  household,
  hasHousehold,
  membershipReady,
  members,
  userId,
  signupRoles,
  selfRoles,
  rolesFor,
  assign,
  assignHousehold,
  unassign,
  verifyOverlaps,
  termsAccepted,
  isAuthenticated,
} = useEventActivities(
  () => props.eventId,
  () => !!props.terms,
)

interface Cluster {
  start: Date
  items: TimelineActivity[]
}

const assignmentByActivity = computed(() => {
  const map = new Map<string, { status: string; roleName: string }>()
  for (const a of assigned.data.value ?? []) {
    if (a.activityId) {
      map.set(a.activityId, { status: a.status, roleName: a.roleName })
    }
  }
  return map
})

const householdByActivity = computed(() => {
  const map = new Map<string, TimelineMemberAssignment[]>()
  for (const a of household.data.value ?? []) {
    if (!a.activityId) continue
    const list = map.get(a.activityId) ?? []
    list.push({
      userId: a.userId,
      name: a.name,
      roleName: a.roleName,
      status: a.status,
    })
    map.set(a.activityId, list)
  }
  return map
})

const items = computed<TimelineActivity[]>(() =>
  (activities.data.value ?? []).map((a) => ({
    id: a.id,
    title: a.title,
    description: a.description,
    location: a.location,
    modality: a.modality,
    start: a.startsAt ? new Date(a.startsAt) : null,
    end: a.endsAt ? new Date(a.endsAt) : null,
    highDemandRoleIds: membershipReady.value ? [...a.highDemandRoleIds] : [],
    assignment: assignmentByActivity.value.get(a.id) ?? null,
    household: householdByActivity.value.get(a.id) ?? [],
  })),
)

const scheduled = computed(() =>
  items.value.filter((a): a is TimelineActivity & { start: Date } => a.start !== null),
)

const unscheduled = computed(() => items.value.filter((a) => a.start === null))

const clusters = computed<Cluster[]>(() => {
  const result: Cluster[] = []
  let current: Cluster | null = null
  let maxEnd = 0
  for (const act of scheduled.value) {
    const start = act.start.getTime()
    const end = (act.end ?? act.start).getTime()
    if (current && start < maxEnd) {
      current.items.push(act)
      maxEnd = Math.max(maxEnd, end)
    } else {
      current = { start: act.start, items: [act] }
      result.push(current)
      maxEnd = end
    }
  }
  return result
})

const busyId = ref<string | null>(null)

const overlapDialog = reactive<{
  visible: boolean
  activity: TimelineActivity | null
  roleId: string
  overlaps: readonly ActivityOverlap[]
}>({ visible: false, activity: null, roleId: '', overlaps: [] })

interface HouseholdRow {
  userId: string
  name: string
  alreadyAssigned: boolean
  assignedRole: string
  include: boolean
  roleId: string
}

const householdDialog = reactive<{
  visible: boolean
  activity: TimelineActivity | null
  rows: HouseholdRow[]
}>({ visible: false, activity: null, rows: [] })

type TermsPendingAction =
  | { kind: 'self'; activityId: string; roleId: string }
  | { kind: 'household'; activityId: string; assignments: HouseholdAssignmentInput[] }

const termsDialog = reactive<{ visible: boolean; action: TermsPendingAction | null }>({
  visible: false,
  action: null,
})

const termsPending = computed(() => !!props.terms && termsAccepted.data.value === false)

function handleSignupError(error: unknown, action: TermsPendingAction): void {
  if (error instanceof ApiError && error.code === 'EventTermsAcceptanceRequired') {
    void queryClient.invalidateQueries({ queryKey: eventQueryKeys.detail(props.eventId) })
    void queryClient.invalidateQueries({ queryKey: eventQueryKeys.termsAcceptance(props.eventId) })
    if (props.terms) {
      termsDialog.action = action
      termsDialog.visible = true
      return
    }
  }
  feedback.error(error, t('pages.eventDetail.toast.signupFailed'))
}

function goLogin(): void {
  void router.push({ name: 'login', query: { redirect: `/events/${props.eventId}` } })
}

async function onSignup(activity: TimelineActivity, roleId: string): Promise<void> {
  if (!props.signupOpen) return
  busyId.value = activity.id
  try {
    const overlap = await verifyOverlaps(activity.id)
    if (overlap?.hasOverlaps) {
      overlapDialog.activity = activity
      overlapDialog.roleId = roleId
      overlapDialog.overlaps = overlap.overlaps ?? []
      overlapDialog.visible = true
      busyId.value = null
      return
    }
    doAssign(activity.id, roleId)
  } catch (error) {
    busyId.value = null
    feedback.error(error)
  }
}

function confirmOverlapSignup(): void {
  if (!overlapDialog.activity) return
  doAssign(overlapDialog.activity.id, overlapDialog.roleId)
  overlapDialog.visible = false
}

function doAssign(activityId: string, roleId: string): void {
  if (termsPending.value) {
    termsDialog.action = { kind: 'self', activityId, roleId }
    termsDialog.visible = true
    busyId.value = null
    return
  }
  executeAssign(activityId, roleId, false)
}

function executeAssign(activityId: string, roleId: string, acceptTerms: boolean): void {
  busyId.value = activityId
  assign.mutate(
    { activityId, activityRoleTypeId: roleId, acceptTerms },
    {
      onSuccess: () =>
        feedback.success(
          t('pages.eventDetail.toast.signupSuccess'),
          t('pages.eventDetail.toast.signupSent'),
        ),
      onError: (error) => handleSignupError(error, { kind: 'self', activityId, roleId }),
      onSettled: () => {
        busyId.value = null
      },
    },
  )
}

function confirmTerms(): void {
  const action = termsDialog.action
  termsDialog.visible = false
  termsDialog.action = null
  if (!action) return
  if (action.kind === 'self') {
    executeAssign(action.activityId, action.roleId, true)
    return
  }
  mutateHousehold(action.activityId, action.assignments, true)
}

function openHousehold(activity: TimelineActivity): void {
  householdDialog.activity = activity
  householdDialog.rows = members.value.map((member) => {
    const existing = activity.household.find((h) => h.userId === member.id)
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

const householdSelectable = computed(() =>
  householdDialog.rows.filter((row) => !row.alreadyAssigned),
)

const householdHighDemand = computed(() => {
  const saturated = householdDialog.activity?.highDemandRoleIds ?? []
  return householdDialog.rows.some(
    (row) => row.include && !row.alreadyAssigned && !!row.roleId && saturated.includes(row.roleId),
  )
})

function confirmHousehold(): void {
  const activity = householdDialog.activity
  if (!activity) return

  const includedRows = householdDialog.rows.filter((row) => row.include && !row.alreadyAssigned)
  const missingRole = includedRows.some((row) => !row.roleId)
  if (missingRole) {
    feedback.warn(
      t('pages.eventDetail.toast.missingRoleDetail'),
      t('pages.eventDetail.toast.missingRole'),
    )
    return
  }

  const assignments = includedRows.map((row) => ({ userId: row.userId, roleId: row.roleId }))

  if (assignments.length === 0) {
    householdDialog.visible = false
    return
  }

  if (termsPending.value) {
    termsDialog.action = { kind: 'household', activityId: activity.id, assignments }
    termsDialog.visible = true
    return
  }

  mutateHousehold(activity.id, assignments, false)
}

function mutateHousehold(
  activityId: string,
  assignments: HouseholdAssignmentInput[],
  acceptTerms: boolean,
): void {
  busyId.value = activityId
  assignHousehold.mutate(
    { activityId, assignments, acceptTerms },
    {
      onSuccess: () => {
        householdDialog.visible = false
        feedback.success(
          t('pages.eventDetail.toast.householdSuccess'),
          t('pages.eventDetail.toast.signupSent'),
        )
      },
      onError: (error) => handleSignupError(error, { kind: 'household', activityId, assignments }),
      onSettled: () => {
        busyId.value = null
      },
    },
  )
}

function onUnassignMember(activity: TimelineActivity, memberId: string): void {
  if (!props.signupOpen) return
  busyId.value = activity.id
  unassign.mutate(
    { activityId: activity.id, userId: memberId },
    {
      onSuccess: () =>
        feedback.success(
          t('pages.eventDetail.toast.unassignSuccess'),
          t('pages.eventDetail.toast.unassignSummary'),
        ),
      onError: (error) => feedback.error(error),
      onSettled: () => {
        busyId.value = null
      },
    },
  )
}

function onUnassign(activity: TimelineActivity): void {
  if (!userId.value) return
  onUnassignMember(activity, userId.value)
}
</script>

<template>
  <div class="activities">
    <p v-if="activities.isLoading.value" class="activities__state">
      {{ $t('pages.eventDetail.activities.loading') }}
    </p>
    <p v-else-if="activities.isError.value" class="activities__state">
      {{ $t('pages.eventDetail.activities.loadError') }}
    </p>
    <p v-else-if="items.length === 0" class="activities__state">
      {{ $t('pages.eventDetail.activities.empty') }}
    </p>

    <template v-else>
      <p v-if="!signupOpen" class="signup-closed">
        <AppIcon name="info-circle" />
        {{
          earlyOnly
            ? $t('pages.eventDetail.activities.earlySignupOnly')
            : $t('pages.eventDetail.activities.signupClosed')
        }}
      </p>
      <p v-else-if="isAuthenticated && signupRoles.isError.value" class="signup-closed">
        <AppIcon name="info-circle" /> {{ $t('pages.eventDetail.activities.rolesLoadError') }}
        <Button
          :label="$t('common.retry')"
          type="primary"
          size="small"
          text
          :loading="signupRoles.isFetching.value"
          @click="signupRoles.refetch()"
        />
      </p>

      <ol class="timeline">
        <li v-for="(cluster, index) in clusters" :key="index" class="tl-node">
          <div class="tl-rail">
            <span class="tl-dot" />
          </div>
          <div class="tl-content">
            <div class="tl-time">
              {{ formatDateTime(cluster.start.toISOString()) }}
              <span v-if="cluster.items.length > 1" class="tl-simul">
                · {{ $t('pages.eventDetail.simultaneous', cluster.items.length) }}
              </span>
            </div>
            <div class="tl-cards" :class="{ 'tl-cards--multi': cluster.items.length > 1 }">
              <ActivityTimelineCard
                v-for="act in cluster.items"
                :key="act.id"
                :activity="act"
                :roles="selfRoles"
                :roles-loading="signupRoles.isLoading.value"
                :reference-date="cluster.start"
                :authenticated="isAuthenticated"
                :signup-open="signupOpen"
                :early-only="earlyOnly"
                :has-household="hasHousehold"
                :busy="busyId === act.id"
                @signup="onSignup(act, $event)"
                @household="openHousehold(act)"
                @unassign="onUnassign(act)"
                @unassign-member="onUnassignMember(act, $event)"
                @login="goLogin"
              />
            </div>
          </div>
        </li>
      </ol>

      <section v-if="unscheduled.length" class="unscheduled">
        <h3 class="unscheduled__title">{{ $t('pages.eventDetail.activities.noSchedule') }}</h3>
        <div class="tl-cards tl-cards--multi">
          <ActivityTimelineCard
            v-for="act in unscheduled"
            :key="act.id"
            :activity="act"
            :roles="selfRoles"
            :roles-loading="signupRoles.isLoading.value"
            :authenticated="isAuthenticated"
            :signup-open="signupOpen"
            :early-only="earlyOnly"
            :has-household="hasHousehold"
            :busy="busyId === act.id"
            @signup="onSignup(act, $event)"
            @household="openHousehold(act)"
            @unassign="onUnassign(act)"
            @unassign-member="onUnassignMember(act, $event)"
            @login="goLogin"
          />
        </div>
      </section>
    </template>

    <el-dialog
      v-model="householdDialog.visible"
      :title="$t('pages.eventDetail.household.header')"
      width="90vw"
      class="household-dialog"
    >
      <p class="household__lead">
        {{ $t('pages.eventDetail.household.leadBefore') }}
        <b>{{ householdDialog.activity?.title }}</b>
        {{ $t('pages.eventDetail.household.leadAfter') }}
      </p>
      <ul class="household__list">
        <li v-for="row in householdDialog.rows" :key="row.userId" class="household__row">
          <div class="household__member">
            <el-checkbox
              v-if="!row.alreadyAssigned"
              :id="`hh-${row.userId}`"
              v-model="row.include"
            />
            <label :for="`hh-${row.userId}`" class="household__name">{{ row.name }}</label>
          </div>
          <span v-if="row.alreadyAssigned" class="household__already">
            {{ $t('pages.eventDetail.household.alreadyAs', { role: row.assignedRole || '—' }) }}
          </span>
          <el-select
            v-else
            v-model="row.roleId"
            :placeholder="$t('pages.eventDetail.chooseRole')"
            :disabled="!row.include"
            class="household__role"
          >
            <el-option
              v-for="role in rolesFor(row.userId)"
              :key="role.id"
              :label="role.name"
              :value="role.id"
            />
          </el-select>
        </li>
      </ul>
      <p v-if="householdSelectable.length === 0" class="household__note">
        {{ $t('pages.eventDetail.household.allInscribed') }}
      </p>
      <p v-if="householdHighDemand" class="household__demand">
        <AppIcon name="exclamation-triangle" />
        <span>{{ $t('pages.eventDetail.highDemandWarning') }}</span>
      </p>
      <template #footer>
        <Button :label="$t('common.cancel')" text @click="householdDialog.visible = false" />
        <Button
          :label="$t('pages.eventDetail.household.enroll')"
          type="primary"
          :disabled="householdSelectable.length === 0"
          @click="confirmHousehold"
        />
      </template>
    </el-dialog>

    <el-dialog
      v-model="overlapDialog.visible"
      :title="$t('pages.eventDetail.overlap.header')"
      width="90vw"
      class="overlap-dialog"
    >
      <p class="overlap__lead">
        {{ $t('pages.eventDetail.overlap.lead') }}
      </p>
      <ul class="overlap__list">
        <li v-for="o in overlapDialog.overlaps" :key="o.activityId">
          <strong>{{ o.title }}</strong>
          <span class="overlap__when">
            {{ formatDateTimeRange(o.startsAt, o.endsAt) }}
          </span>
        </li>
      </ul>
      <p class="overlap__q">{{ $t('pages.eventDetail.overlap.question') }}</p>
      <template #footer>
        <Button :label="$t('common.cancel')" text @click="overlapDialog.visible = false" />
        <Button
          :label="$t('pages.eventDetail.overlap.enrollAnyway')"
          type="primary"
          @click="confirmOverlapSignup"
        />
      </template>
    </el-dialog>

    <el-dialog
      v-model="termsDialog.visible"
      :title="$t('pages.eventDetail.terms.header')"
      width="min(680px, 94vw)"
      append-to-body
    >
      <p class="terms__lead">
        {{ $t('pages.eventDetail.terms.lead') }}
        <b>{{ terms?.name }}</b>
      </p>
      <div class="terms__content">
        <RichTextContent :content="terms?.description ?? ''" />
      </div>
      <p class="terms__q">{{ $t('pages.eventDetail.terms.question') }}</p>
      <template #footer>
        <Button :label="$t('common.cancel')" text @click="termsDialog.visible = false" />
        <Button
          :label="$t('pages.eventDetail.terms.accept')"
          type="primary"
          @click="confirmTerms"
        />
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.activities__state {
  padding: 12px 0 24px;
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.signup-closed {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 20px;
  padding: 12px 16px;
  border: 1px solid var(--ca-warning);
  background: var(--ca-warning-soft);
  border-radius: 12px;
  color: var(--ca-text);
  font-size: 14.5px;
}

.signup-closed .app-icon {
  color: var(--ca-warning);
}

.activities :deep(.household-dialog) {
  max-width: 560px;
}

.activities :deep(.overlap-dialog) {
  max-width: 480px;
}

.terms__lead {
  color: var(--ca-text);
  line-height: 1.55;
  margin-bottom: 14px;
}

.terms__content {
  max-height: 48vh;
  overflow-y: auto;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 14px 16px;
  margin-bottom: 14px;
}

.terms__q {
  color: var(--ca-text);
  font-weight: 600;
}

.timeline {
  list-style: none;
  margin: 8px 0 0;
  padding: 0;
}

.tl-node {
  display: grid;
  grid-template-columns: 24px 1fr;
  gap: 16px;
}

.tl-rail {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.tl-dot {
  width: 14px;
  height: 14px;
  margin-top: 4px;
  border-radius: 50%;
  background: var(--ca-orange);
  box-shadow: 0 0 0 4px var(--ca-orange-soft);
}

.tl-rail::after {
  content: '';
  flex: 1;
  width: 2px;
  margin-top: 6px;
  background: var(--ca-border-strong);
}

.tl-node:last-child .tl-rail::after {
  display: none;
}

.tl-content {
  padding-bottom: 26px;
  min-width: 0;
}

.tl-time {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text);
  margin-bottom: 10px;
}

.tl-simul {
  color: var(--ca-warning-ink);
}

.tl-cards {
  display: grid;
  gap: 14px;
}

.tl-cards--multi {
  grid-template-columns: repeat(auto-fit, minmax(min(260px, 100%), 1fr));
}

.unscheduled {
  margin-top: 16px;
}

.unscheduled__title {
  font-family: var(--ca-font-display);
  font-size: 18px;
  font-weight: 600;
  color: var(--ca-text-bright);
  margin-bottom: 14px;
}

.household__lead {
  color: var(--ca-text);
  line-height: 1.55;
  margin-bottom: 16px;
}

.household__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.household__row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 12px 14px;
}

.household__member {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.household__name {
  font-weight: 600;
  color: var(--ca-text-bright);
  cursor: pointer;
}

.household__already {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.household__role {
  min-width: 170px;
  width: auto;
  max-width: 100%;
}

.household__note {
  margin-top: 14px;
  font-size: 13.5px;
  color: var(--ca-text-muted);
}

.household__demand {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  margin: 14px 0 0;
  padding: 8px 10px;
  border-radius: 10px;
  background: var(--ca-warning-soft);
  color: var(--ca-warning-ink);
  font-size: 13px;
  line-height: 1.45;
}

.household__demand .app-icon {
  margin-top: 2px;
  font-size: 13px;
}

.overlap__lead {
  color: var(--ca-text);
  margin-bottom: 12px;
}

.overlap__list {
  list-style: none;
  margin: 0 0 16px;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.overlap__list li {
  display: flex;
  flex-direction: column;
  gap: 2px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 10px;
  padding: 10px 12px;
}

.overlap__when {
  font-family: var(--ca-font-mono);
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.overlap__q {
  color: var(--ca-text);
  font-weight: 600;
}
@media (max-width: 640px) {
  .household__row {
    flex-direction: column;
    align-items: stretch;
    gap: 10px;
  }

  .household__role {
    min-width: 0;
    width: 100%;
  }
}
</style>
