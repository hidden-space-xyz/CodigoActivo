<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { DataState } from '@/shared/ui/data-state'
import { PrintToolbar } from '@/shared/ui/print-toolbar'

import { rosterQueries } from '../api/queries'
import { measureRoster } from '../lib/measure-roster'
import { paginateRoster, type SheetChunk } from '../lib/paginate-roster'
import { rosterRows } from '../lib/roster-rows'
import RosterChunk from './RosterChunk.vue'

const props = defineProps<{
  /** Event id from the `/admin/events/:eventId/roster` route param. */
  eventId: string
}>()

const report = useQuery(() => rosterQueries.ofEvent(props.eventId))
const eventTitle = computed(() => report.data.value?.title ?? '')
const activities = computed(() => report.data.value?.activities ?? [])

const sheets = ref<SheetChunk[][]>([])
const measureEl = ref<HTMLElement | null>(null)

async function paginate(): Promise<void> {
  await nextTick()
  await document.fonts.ready
  const container = measureEl.value
  sheets.value = container
    ? paginateRoster(activities.value, measureRoster(container, activities.value.length))
    : []
}

watch(activities, () => void paginate(), { immediate: true })
</script>

<template>
  <div class="roster">
    <PrintToolbar
      :back="{ name: 'admin-event-detail', params: { eventId } }"
      :back-label="$t('pages.admin.eventRoster.back')"
      :print-label="$t('pages.admin.eventRoster.print')"
    />

    <DataState
      class="no-print"
      :loading="report.isLoading.value"
      :error="report.isError.value"
      :empty="activities.length === 0"
      :empty-text="$t('pages.admin.eventRoster.emptyText')"
    >
      <span />
    </DataState>

    <div v-for="(sheet, sheetIndex) in sheets" :key="sheetIndex" class="sheet">
      <p class="sheet__event">
        {{ $t('pages.admin.eventRoster.sheetHeader', { title: eventTitle }) }}
      </p>
      <RosterChunk
        v-for="chunk in sheet"
        :key="`${chunk.activity.id}-${chunk.continued ? 'cont' : 'start'}`"
        :activity="chunk.activity"
        :rows="chunk.rows"
        :continued="chunk.continued"
      />
    </div>

    <div ref="measureEl" class="sheet measure" aria-hidden="true">
      <p class="sheet__event" data-part="sheet-head">
        {{ $t('pages.admin.eventRoster.sheetHeader', { title: eventTitle }) }}
      </p>
      <RosterChunk
        v-for="(activity, index) in activities"
        :key="activity.id"
        :activity="activity"
        :rows="rosterRows(activity)"
        :continued="false"
        :data-activity-index="index"
      />
    </div>
  </div>
</template>

<style scoped>
.roster {
  min-height: 100vh;
  padding: 24px 16px 48px;
}

.sheet {
  box-sizing: border-box;
  width: 210mm;
  min-height: 297mm;
  margin: 0 auto 24px;
  padding: 8mm;
  background: #fff;
  color: #111827;
  box-shadow: 0 4px 24px rgb(0 0 0 / 0.5);
  print-color-adjust: exact;
  -webkit-print-color-adjust: exact;
}

.measure {
  position: absolute;
  left: -9999px;
  top: 0;
  min-height: 0;
  margin: 0;
  box-shadow: none;
  visibility: hidden;
  pointer-events: none;
}

.sheet__event {
  margin: 0;
  padding-bottom: 2.5mm;
  font-size: 8pt;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #4b5563;
}

@media print {
  .no-print,
  .measure {
    display: none !important;
  }

  .roster {
    min-height: 0;
    padding: 0;
  }

  .sheet {
    min-height: 0;
    margin: 0;
    box-shadow: none;
    break-after: page;
  }

  .sheet:last-child {
    break-after: auto;
  }
}

@media screen and (max-width: 1024px) {
  .roster {
    padding: 16px var(--ca-gutter) 40px;
    overflow-x: auto;
  }
}
</style>
