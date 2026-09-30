<script setup lang="ts">
import { computed } from 'vue'

import type { EventTermsSummary } from '@/entities/event'
import { formatDateTime, formatDateTimeRange } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AppIcon } from '@/shared/ui/app-icon'

import { toTimeline, toTimelineActivities } from '../lib/timeline'
import { useEventActivities } from '../model/use-event-activities'
import { useSignupFlow } from '../model/use-signup-flow'
import ActivityTimelineCard from './ActivityTimelineCard.vue'
import EventTermsDialog from './EventTermsDialog.vue'

const props = defineProps<{
  /** Event whose activities are listed and grouped by overlapping schedules. */
  eventId: string
  /** Whether the current user may enroll, including early signup when they are eligible. */
  signupOpen: boolean
  /** Only early signup is open and the user is not eligible; picks the closed-signup message. */
  earlyOnly?: boolean
  /** Terms documents linked to the event; empty when there are none. */
  terms?: readonly EventTermsSummary[]
}>()

const hasTerms = (): boolean => (props.terms?.length ?? 0) > 0
const eventActivities = useEventActivities(() => props.eventId, hasTerms)
const {
  activities,
  assigned,
  household,
  hasHousehold,
  membershipReady,
  signupRoles,
  selfRoles,
  rolesFor,
  isAuthenticated,
} = eventActivities
const {
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
} = useSignupFlow({
  eventId: () => props.eventId,
  signupOpen: () => props.signupOpen,
  hasTerms,
  activities: eventActivities,
})

const items = computed(() =>
  toTimelineActivities(
    activities.data.value ?? [],
    assigned.data.value ?? [],
    household.data.value ?? [],
    membershipReady.value,
  ),
)
const clusters = computed(() => toTimeline(items.value))
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
        <ActionButton
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
            {{ $t('pages.eventDetail.household.alreadyAs', { role: row.assignedRole }) }}
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
        <ActionButton :label="$t('common.cancel')" text @click="householdDialog.visible = false" />
        <ActionButton
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
        <ActionButton :label="$t('common.cancel')" text @click="overlapDialog.visible = false" />
        <ActionButton
          :label="$t('pages.eventDetail.overlap.enrollAnyway')"
          type="primary"
          @click="confirmOverlapSignup"
        />
      </template>
    </el-dialog>

    <EventTermsDialog
      v-model:visible="termsDialog.visible"
      :documents="pendingTermsDocuments"
      @confirm="confirmTerms"
    />
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
