<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { useAccountHistory } from '../model/useAccountHistory'
import EventRatingDialog from './EventRatingDialog.vue'
import type { AccountHistoryEntry, EventRatingInput } from '@/entities/account'
import { AppIcon, BaseButton } from '@/shared/ui'
import { formatDateRange, useCrudFeedback } from '@/shared/lib'

const { t } = useI18n()
const feedback = useCrudFeedback()
const { history, entries, upcoming, past, saveRating } = useAccountHistory()

const expanded = ref<Record<string, boolean>>({})

function toggle(eventId: string): void {
  expanded.value[eventId] = !expanded.value[eventId]
}

function isExpanded(eventId: string): boolean {
  return expanded.value[eventId] === true
}

function statusSeverity(name: string): 'success' | 'danger' | 'info' {
  const value = name.toLowerCase()
  if (value.includes('confirm') || value.includes('acept') || value.includes('aprob'))
    return 'success'
  if (value.includes('rechaz') || value.includes('deneg') || value.includes('cancel'))
    return 'danger'
  return 'info'
}

const ratingTarget = ref<AccountHistoryEntry | null>(null)

function openRating(entry: AccountHistoryEntry): void {
  ratingTarget.value = entry
}

function submitRating(input: EventRatingInput): void {
  const eventId = ratingTarget.value?.eventId
  if (!eventId) return
  saveRating.mutate(
    { eventId, input },
    {
      onSuccess: () => {
        ratingTarget.value = null
        feedback.success(
          t('features.account.history.savedDetail'),
          t('features.account.history.savedSummary'),
        )
      },
      onError: (error) => feedback.error(error),
    },
  )
}

const groups = computed(() => [
  { key: 'upcoming', label: t('features.account.history.upcoming'), items: upcoming.value },
  { key: 'past', label: t('features.account.history.past'), items: past.value },
])
</script>

<template>
  <section class="acc-pane">
    <div class="acc-pane__head">
      <p class="acc-pane__lead">{{ $t('features.account.history.lead') }}</p>
    </div>

    <p v-if="history.isLoading.value" class="acc-pane__state">{{ $t('common.loading') }}</p>
    <p v-else-if="history.isError.value" class="acc-pane__state">
      {{ $t('features.account.history.error') }}
    </p>
    <p v-else-if="entries.length === 0" class="acc-pane__state">
      {{ $t('features.account.history.empty') }}
    </p>

    <template v-else>
      <div v-for="group in groups" :key="group.key" class="acc-history__group">
        <template v-if="group.items.length > 0">
          <h3 class="acc-history__group-title">{{ group.label }}</h3>

          <ul class="acc-history__events">
            <li v-for="entry in group.items" :key="entry.eventId" class="acc-history__event">
              <div class="acc-history__head">
                <button
                  type="button"
                  class="acc-history__toggle"
                  :aria-expanded="isExpanded(entry.eventId)"
                  @click="toggle(entry.eventId)"
                >
                  <AppIcon
                    class="acc-history__chevron"
                    :name="isExpanded(entry.eventId) ? 'chevron-down' : 'chevron-right'"
                  />
                  <span class="acc-history__info">
                    <span class="acc-history__name">{{ entry.title }}</span>
                    <span class="acc-history__meta">
                      {{ formatDateRange(entry.startsAt, entry.endsAt) }} ·
                      {{ $t('features.account.history.activityCount', entry.activities.length) }}
                    </span>
                  </span>
                </button>

                <div v-if="entry.canRate" class="acc-history__actions">
                  <span v-if="entry.rating" class="acc-history__score">
                    <AppIcon name="star-fill" />
                    {{ entry.rating.score }}/5
                  </span>
                  <BaseButton variant="ghost" @click="openRating(entry)">
                    {{
                      entry.rating
                        ? $t('features.account.history.editRating')
                        : $t('features.account.history.rate')
                    }}
                  </BaseButton>
                </div>
              </div>

              <ul v-if="isExpanded(entry.eventId)" class="acc-history__activities">
                <li
                  v-for="activity in entry.activities"
                  :key="`${activity.activityId}-${activity.participantId}`"
                  class="acc-history__activity"
                >
                  <span class="acc-history__activity-title">{{ activity.title }}</span>
                  <span class="acc-history__activity-meta">
                    <span v-if="!activity.isSelf" class="acc-history__participant">
                      {{ activity.participantName }}
                    </span>
                    <span v-if="activity.roleName">{{ activity.roleName }}</span>
                  </span>
                  <el-tag
                    v-if="!entry.isPast && activity.statusName"
                    :type="statusSeverity(activity.statusName)"
                  >
                    {{ activity.statusName }}
                  </el-tag>
                </li>
              </ul>
            </li>
          </ul>
        </template>
      </div>
    </template>

    <EventRatingDialog
      :visible="ratingTarget !== null"
      :event-title="ratingTarget?.title ?? ''"
      :rating="ratingTarget?.rating ?? null"
      :saving="saveRating.isPending.value"
      @submit="submitRating"
      @close="ratingTarget = null"
    />
  </section>
</template>

<style scoped>
.acc-pane__head {
  margin-bottom: 18px;
}

.acc-pane__lead {
  font-size: 14px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  max-width: 62ch;
}

.acc-pane__state {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.acc-history__group + .acc-history__group {
  margin-top: 22px;
}

.acc-history__group-title {
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--ca-text-muted);
  margin-bottom: 10px;
}

.acc-history__events {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.acc-history__event {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  overflow: hidden;
}

.acc-history__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
  padding: 14px 16px;
}

.acc-history__toggle {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
  min-width: 0;
  background: none;
  border: none;
  padding: 0;
  text-align: left;
  cursor: pointer;
  color: inherit;
  font: inherit;
}

.acc-history__toggle .acc-history__chevron {
  font-size: 12px;
  color: var(--ca-text-muted);
  flex-shrink: 0;
}

.acc-history__info {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.acc-history__name {
  font-weight: 600;
  color: var(--ca-text-bright);
}

.acc-history__meta {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.acc-history__actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
}

.acc-history__score {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-orange-ink);
}

.acc-history__activities {
  list-style: none;
  margin: 0;
  padding: 0 16px 14px 40px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.acc-history__activity {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  border-top: 1px solid var(--ca-border-soft);
  padding-top: 8px;
}

.acc-history__activity-title {
  font-weight: 600;
  color: var(--ca-text);
}

.acc-history__activity-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  font-size: 13px;
  color: var(--ca-text-muted);
}

.acc-history__participant {
  color: var(--ca-text);
  font-weight: 600;
}

@media (max-width: 640px) {
  .acc-history__actions {
    width: 100%;
    justify-content: flex-end;
  }
}
</style>
