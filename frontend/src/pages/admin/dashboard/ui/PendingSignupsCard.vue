<script setup lang="ts">
import { formatDate } from '@/shared/lib/date'
import { formatNumber } from '@/shared/lib/number'
import { ChartCard } from '@/shared/ui/chart'

import type { PendingSignups } from '../model/types'

defineProps<{
  /**
   * Requested signups of upcoming activities, overall and per event with the soonest activity
   * first; each event links to its management page, where they are confirmed or denied.
   */
  pending: PendingSignups
}>()
</script>

<template>
  <ChartCard
    :title="$t('pages.admin.dashboard.pending.title')"
    :subtitle="$t('pages.admin.dashboard.pending.subtitle')"
  >
    <div v-if="pending.events.length === 0" class="pending__empty">
      {{ $t('pages.admin.dashboard.pending.empty') }}
    </div>

    <template v-else>
      <p class="pending__total">
        {{
          $t(
            'pages.admin.dashboard.pending.summary',
            { n: formatNumber(pending.total) },
            pending.total,
          )
        }}
      </p>
      <ul class="pending__list">
        <li v-for="event in pending.events" :key="event.id" class="pending__event">
          <RouterLink
            :to="{ name: 'admin-event-detail', params: { eventId: event.id } }"
            class="pending__link"
          >
            <span class="pending__info">
              <span class="pending__name" :title="event.title">{{ event.title }}</span>
              <span class="pending__meta">{{
                $t('pages.admin.dashboard.pending.meta', {
                  next: formatDate(event.nextActivityStartsAt),
                  oldest: formatDate(event.oldestRequestedAt),
                })
              }}</span>
            </span>
            <span class="pending__count">{{
              $t(
                'pages.admin.dashboard.pending.count',
                { n: formatNumber(event.pending) },
                event.pending,
              )
            }}</span>
          </RouterLink>
        </li>
      </ul>
    </template>
  </ChartCard>
</template>

<style scoped>
.pending__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 120px;
  color: var(--ca-text-dim);
  font-size: 13.5px;
}

.pending__total {
  font-family: var(--ca-font-display);
  font-size: 15px;
  font-weight: 600;
  color: var(--ca-text-bright);
  margin-bottom: 8px;
}

.pending__list {
  list-style: none;
  max-height: 300px;
  overflow-y: auto;
}

.pending__event {
  border-top: 1px solid var(--ca-border-soft);
}

.pending__event:first-child {
  border-top: none;
}

.pending__link {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 2px;
  color: inherit;
  text-decoration: none;
}

.pending__link:hover {
  background: var(--ca-surface-2);
}

.pending__info {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.pending__name {
  font-size: 14px;
  color: var(--ca-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pending__meta {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.pending__count {
  flex-shrink: 0;
  font-family: var(--ca-font-mono);
  font-size: 12px;
  font-weight: 600;
  color: var(--ca-warning-ink);
  background: var(--ca-warning-soft);
  padding: 2px 10px;
  border-radius: 999px;
}

@media (max-width: 640px) {
  .pending__name {
    white-space: normal;
    overflow: visible;
  }
}
</style>
