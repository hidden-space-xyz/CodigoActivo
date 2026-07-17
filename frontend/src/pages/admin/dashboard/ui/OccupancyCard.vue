<script setup lang="ts">
import { computed, ref } from 'vue'

import { formatDateTime, formatNumber } from '@/shared/lib'
import type { DashboardOccupancyResponse } from '@/shared/api/generated/models'

import ChartCard from './ChartCard.vue'

const props = defineProps<{ occupancy: DashboardOccupancyResponse }>()

const events = computed(() => props.occupancy.events ?? [])
const overall = computed(() => {
  const desired = props.occupancy.desired ?? 0
  return desired > 0 ? Math.round(((props.occupancy.confirmed ?? 0) / desired) * 100) : null
})

const expanded = ref<Record<string, boolean>>({})
function toggle(id: string | undefined): void {
  if (!id) return
  expanded.value[id] = !expanded.value[id]
}

function percent(confirmed?: number, desired?: number): number | null {
  const d = desired ?? 0
  return d > 0 ? Math.round(((confirmed ?? 0) / d) * 100) : null
}
</script>

<template>
  <ChartCard
    title="Ocupación de próximas actividades"
    subtitle="Plazas confirmadas frente al aforo previsto · pulsa un evento para ver sus actividades"
  >
    <p v-if="overall !== null" class="occupancy__overall">
      Media global <strong>{{ overall }}%</strong> · {{ formatNumber(occupancy.confirmed) }} de
      {{ formatNumber(occupancy.desired) }} plazas
    </p>

    <div v-if="events.length === 0" class="occupancy__empty">
      No hay próximas actividades con aforo definido.
    </div>

    <ul v-else class="occupancy__list">
      <li v-for="event in events" :key="event.eventId" class="occupancy__event">
        <button type="button" class="occupancy__row" @click="toggle(event.eventId)">
          <i
            class="occupancy__chevron pi"
            :class="expanded[event.eventId ?? ''] ? 'pi-chevron-down' : 'pi-chevron-right'"
            aria-hidden="true"
          />
          <span class="occupancy__name" :title="event.title ?? ''">{{ event.title }}</span>
          <span class="occupancy__meter">
            <span
              class="occupancy__fill"
              :class="{
                'occupancy__fill--over': (percent(event.confirmed, event.desired) ?? 0) > 100,
              }"
              :style="{ width: `${Math.min(percent(event.confirmed, event.desired) ?? 0, 100)}%` }"
            />
          </span>
          <span class="occupancy__pct">{{ percent(event.confirmed, event.desired) ?? '—' }}%</span>
        </button>

        <ul v-if="expanded[event.eventId ?? '']" class="occupancy__activities">
          <li
            v-for="activity in event.activities ?? []"
            :key="activity.activityId"
            class="occupancy__activity"
          >
            <span class="occupancy__activity-info">
              <span class="occupancy__activity-name" :title="activity.title ?? ''">{{
                activity.title
              }}</span>
              <span class="occupancy__activity-date">{{ formatDateTime(activity.startsAt) }}</span>
            </span>
            <span class="occupancy__meter occupancy__meter--sm">
              <span
                class="occupancy__fill"
                :class="{
                  'occupancy__fill--over':
                    (percent(activity.confirmed, activity.desired) ?? 0) > 100,
                }"
                :style="{
                  width: `${Math.min(percent(activity.confirmed, activity.desired) ?? 0, 100)}%`,
                }"
              />
            </span>
            <span class="occupancy__activity-pct">
              {{ percent(activity.confirmed, activity.desired) ?? '—' }}%
              <span class="occupancy__activity-plazas"
                >({{ formatNumber(activity.confirmed) }}/{{ formatNumber(activity.desired) }})</span
              >
            </span>
          </li>
        </ul>
      </li>
    </ul>
  </ChartCard>
</template>

<style scoped>
.occupancy__overall {
  font-size: 13px;
  color: var(--ca-text-muted);
  margin-bottom: 12px;
}

.occupancy__overall strong {
  color: var(--ca-text-bright);
  font-family: var(--ca-font-display);
}

.occupancy__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 120px;
  color: var(--ca-text-dim);
  font-size: 13.5px;
}

.occupancy__list {
  list-style: none;
  max-height: 340px;
  overflow-y: auto;
}

.occupancy__event {
  border-top: 1px solid var(--ca-border-soft);
}

.occupancy__event:first-child {
  border-top: none;
}

.occupancy__row {
  display: grid;
  grid-template-columns: 16px minmax(0, 1fr) 90px 46px;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 10px 2px;
  background: transparent;
  border: none;
  cursor: pointer;
  text-align: left;
}

.occupancy__row:hover {
  background: var(--ca-surface-2);
}

.occupancy__chevron {
  font-size: 11px;
  color: var(--ca-text-dim);
}

.occupancy__name {
  font-size: 14px;
  color: var(--ca-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.occupancy__meter {
  height: 8px;
  border-radius: 999px;
  background: var(--ca-surface-2);
  overflow: hidden;
}

.occupancy__meter--sm {
  height: 6px;
}

.occupancy__fill {
  display: block;
  height: 100%;
  border-radius: 999px;
  background: var(--ca-lime);
  transition: width 0.3s ease;
}

.occupancy__fill--over {
  background: var(--ca-warning);
}

.occupancy__pct {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-bright);
  text-align: right;
}

.occupancy__activities {
  list-style: none;
  padding: 4px 0 10px 26px;
}

.occupancy__activity {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 70px 96px;
  align-items: center;
  gap: 10px;
  padding: 5px 0;
}

.occupancy__activity-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.occupancy__activity-name {
  font-size: 13px;
  color: var(--ca-text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.occupancy__activity-date {
  font-size: 11.5px;
  color: var(--ca-text-dim);
}

.occupancy__activity-pct {
  font-family: var(--ca-font-mono);
  font-size: 12.5px;
  color: var(--ca-text);
  text-align: right;
}

.occupancy__activity-plazas {
  color: var(--ca-text-dim);
}
</style>
