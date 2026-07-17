<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { DataState } from '@/shared/ui'

import { useEventRoster } from '@/features/manage-events'
import type {
  EventRosterActivityResponse,
  EventRosterParticipantResponse,
} from '@/shared/api/generated/models'
import { ageFrom, formatDateTimeRange } from '@/shared/lib'

const { t } = useI18n()

const SHEET_WIDTH_MM = 210
const USABLE_HEIGHT_MM = 275
const CHUNK_GAP_MM = 5

const pageStyle = document.createElement('style')
pageStyle.textContent = '@page { size: A4 portrait; margin: 0; }'
onMounted(() => document.head.appendChild(pageStyle))
onBeforeUnmount(() => pageStyle.remove())

const route = useRoute()
const eventId = computed(() => String(route.params.eventId))

const report = useEventRoster(eventId)
const eventTitle = computed(() => report.data.value?.title ?? '')
const rosterActivities = computed(() => report.data.value?.activities ?? [])

interface RoleRow {
  kind: 'role'
  key: string
  label: string
}

interface ParticipantRow {
  kind: 'participant'
  key: string
  participant: EventRosterParticipantResponse
}

type RosterRow = RoleRow | ParticipantRow

interface SheetChunk {
  activity: EventRosterActivityResponse
  rows: RosterRow[]
  continued: boolean
}

function pluralizeRole(name: string): string {
  if (!name) return t('pages.admin.eventRoster.roleFallback')
  return /[aeiouáéíóú]$/i.test(name) ? `${name}s` : `${name}es`
}

function fullNameOf(participant: EventRosterParticipantResponse): string {
  return [participant.firstName, participant.lastName].filter(Boolean).join(' ')
}

function rowsFor(activity: EventRosterActivityResponse): RosterRow[] {
  const rows: RosterRow[] = []
  let currentRole: string | null = null
  for (const participant of activity.participants ?? []) {
    const role = participant.roleName ?? ''
    if (role !== currentRole) {
      currentRole = role
      rows.push({ kind: 'role', key: `role:${role}`, label: pluralizeRole(role) })
    }
    rows.push({ kind: 'participant', key: `p:${participant.userId}`, participant })
  }
  return rows
}

const sheets = ref<SheetChunk[][]>([])
const measureEl = ref<HTMLElement | null>(null)

async function paginate(): Promise<void> {
  await nextTick()
  await document.fonts.ready
  const container = measureEl.value
  if (!container) {
    sheets.value = []
    return
  }

  const pxPerMm = container.getBoundingClientRect().width / SHEET_WIDTH_MM
  const sheetHead =
    container.querySelector('[data-part="sheet-head"]')?.getBoundingClientRect().height ?? 0
  const capacity = USABLE_HEIGHT_MM * pxPerMm - sheetHead
  const gap = CHUNK_GAP_MM * pxPerMm

  const pages: SheetChunk[][] = []
  let page: SheetChunk[] = []
  let used = 0

  const closePage = (): void => {
    if (page.length) pages.push(page)
    page = []
    used = 0
  }

  for (const [index, activity] of rosterActivities.value.entries()) {
    const section = container.querySelector<HTMLElement>(`[data-activity-index="${index}"]`)
    if (!section) continue
    const headHeight =
      section.querySelector('[data-part="head"]')?.getBoundingClientRect().height ?? 0
    const theadHeight = section.querySelector('thead')?.getBoundingClientRect().height ?? 0
    const rowHeights = [...section.querySelectorAll('tbody tr')].map(
      (row) => row.getBoundingClientRect().height,
    )

    let chunk: SheetChunk | null = null
    const rows = rowsFor(activity)
    let lastRole: RoleRow | null = null
    let lastRoleHeight = 0

    for (const [rowIndex, row] of rows.entries()) {
      const rowHeight = rowHeights[rowIndex] ?? 0
      const needed = row.kind === 'role' ? rowHeight + (rowHeights[rowIndex + 1] ?? 0) : rowHeight
      if (!chunk) {
        const total = (page.length ? gap : 0) + headHeight + theadHeight + needed
        if (page.length && used + total > capacity) closePage()
        if (page.length) used += gap
        chunk = { activity, rows: [], continued: false }
        page.push(chunk)
        used += headHeight + theadHeight
      } else if (used + needed > capacity && chunk.rows.length) {
        closePage()
        chunk = { activity, rows: [], continued: true }
        page.push(chunk)
        used += headHeight + theadHeight
        if (row.kind === 'participant' && lastRole) {
          chunk.rows.push({ ...lastRole, key: `${lastRole.key}:cont` })
          used += lastRoleHeight
        }
      }
      chunk.rows.push(row)
      used += rowHeight
      if (row.kind === 'role') {
        lastRole = row
        lastRoleHeight = rowHeight
      }
    }
  }
  closePage()

  sheets.value = pages
}

watch(rosterActivities, () => void paginate(), { immediate: true })

function ageLabel(participant: EventRosterParticipantResponse): string {
  const age = ageFrom(participant.birthDate)
  return age === null ? '—' : t('pages.admin.eventRoster.ageYears', { age })
}

function participantsLabel(count: number): string {
  return t('pages.admin.eventRoster.participantsCount', { count }, count)
}

function contactLines(participant: EventRosterParticipantResponse): string[] {
  return [participant.phone, participant.email].filter((value): value is string => !!value)
}

function guardianContactLines(participant: EventRosterParticipantResponse): string[] {
  const guardian = participant.guardian
  if (!guardian) return []
  const name = [guardian.firstName, guardian.lastName].filter(Boolean).join(' ')
  return [name, guardian.phone ?? '', guardian.email ?? ''].filter(Boolean)
}
</script>

<template>
  <div class="roster">
    <div class="back-row no-print">
      <RouterLink :to="{ name: 'admin-event-detail', params: { eventId } }" class="back">
        {{ $t('pages.admin.eventRoster.back') }}
      </RouterLink>
    </div>

    <DataState
      class="no-print"
      :loading="report.isLoading.value"
      :error="report.isError.value"
      :empty="rosterActivities.length === 0"
      :empty-text="$t('pages.admin.eventRoster.emptyText')"
    >
      <span />
    </DataState>

    <div v-for="(page, pageIndex) in sheets" :key="pageIndex" class="sheet">
      <p class="sheet__event">{{ $t('pages.admin.eventRoster.sheetHeader', { title: eventTitle }) }}</p>

      <section
        v-for="chunk in page"
        :key="`${chunk.activity.activityId}-${chunk.continued ? 'cont' : 'start'}`"
        class="chunk"
      >
        <header class="chunk__head">
          <h2 class="chunk__title">
            {{ chunk.activity.title
            }}<span v-if="chunk.continued" class="chunk__cont">{{
              $t('pages.admin.eventRoster.continued')
            }}</span>
          </h2>
          <p class="chunk__meta">
            <span>{{
              formatDateTimeRange(chunk.activity.activityStartsAt, chunk.activity.activityEndsAt)
            }}</span>
            <span v-if="chunk.activity.location">· {{ chunk.activity.location }}</span>
            <span>· {{ participantsLabel(chunk.activity.participants?.length ?? 0) }}</span>
          </p>
        </header>

        <table class="list">
          <thead>
            <tr>
              <th class="list__check-col">{{ $t('pages.admin.eventRoster.colAttends') }}</th>
              <th class="list__name-col">{{ $t('pages.admin.eventRoster.colFullName') }}</th>
              <th class="list__age-col">{{ $t('pages.admin.eventRoster.colAge') }}</th>
              <th>{{ $t('pages.admin.eventRoster.colContact') }}</th>
              <th>{{ $t('pages.admin.eventRoster.colGuardian') }}</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="row in chunk.rows" :key="row.key">
              <tr v-if="row.kind === 'role'" class="list__role-row">
                <td colspan="5">{{ row.label }}</td>
              </tr>
              <tr v-else>
                <td class="list__check-col"><span class="list__checkbox" /></td>
                <td class="list__name">{{ fullNameOf(row.participant) || '—' }}</td>
                <td class="list__age-col">{{ ageLabel(row.participant) }}</td>
                <td class="list__contact">
                  <template v-if="contactLines(row.participant).length">
                    <div v-for="line in contactLines(row.participant)" :key="line">
                      {{ line }}
                    </div>
                  </template>
                  <template v-else>—</template>
                </td>
                <td class="list__contact">
                  <template v-if="guardianContactLines(row.participant).length">
                    <div v-for="line in guardianContactLines(row.participant)" :key="line">
                      {{ line }}
                    </div>
                  </template>
                  <template v-else>—</template>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </section>
    </div>

    <div ref="measureEl" class="sheet measure" aria-hidden="true">
      <p class="sheet__event" data-part="sheet-head">{{ $t('pages.admin.eventRoster.sheetHeader', { title: eventTitle }) }}</p>
      <section
        v-for="(activity, index) in rosterActivities"
        :key="activity.activityId"
        class="chunk"
        :data-activity-index="index"
      >
        <header class="chunk__head" data-part="head">
          <h2 class="chunk__title">{{ activity.title }}</h2>
          <p class="chunk__meta">
            <span>{{
              formatDateTimeRange(activity.activityStartsAt, activity.activityEndsAt)
            }}</span>
            <span v-if="activity.location">· {{ activity.location }}</span>
            <span>· {{ participantsLabel(activity.participants?.length ?? 0) }}</span>
          </p>
        </header>

        <table class="list">
          <thead>
            <tr>
              <th class="list__check-col">{{ $t('pages.admin.eventRoster.colAttends') }}</th>
              <th class="list__name-col">{{ $t('pages.admin.eventRoster.colFullName') }}</th>
              <th class="list__age-col">{{ $t('pages.admin.eventRoster.colAge') }}</th>
              <th>{{ $t('pages.admin.eventRoster.colContact') }}</th>
              <th>{{ $t('pages.admin.eventRoster.colGuardian') }}</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="row in rowsFor(activity)" :key="row.key">
              <tr v-if="row.kind === 'role'" class="list__role-row">
                <td colspan="5">{{ row.label }}</td>
              </tr>
              <tr v-else>
                <td class="list__check-col"><span class="list__checkbox" /></td>
                <td class="list__name">{{ fullNameOf(row.participant) || '—' }}</td>
                <td class="list__age-col">{{ ageLabel(row.participant) }}</td>
                <td class="list__contact">
                  <template v-if="contactLines(row.participant).length">
                    <div v-for="line in contactLines(row.participant)" :key="line">
                      {{ line }}
                    </div>
                  </template>
                  <template v-else>—</template>
                </td>
                <td class="list__contact">
                  <template v-if="guardianContactLines(row.participant).length">
                    <div v-for="line in guardianContactLines(row.participant)" :key="line">
                      {{ line }}
                    </div>
                  </template>
                  <template v-else>—</template>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </section>
    </div>
  </div>
</template>

<style scoped>
.roster {
  min-height: 100vh;
  padding: 24px 16px 48px;
}

.back-row {
  max-width: 210mm;
  margin: 0 auto;
}

.back {
  display: inline-block;
  margin-bottom: 14px;
  color: var(--ca-text-muted);
  text-decoration: none;
  font-size: 14px;
}

.back:hover {
  color: var(--ca-text);
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

.chunk + .chunk {
  margin-top: 5mm;
}

.chunk__head {
  margin: 0;
  padding-bottom: 1.6mm;
  border-bottom: 0.5mm solid #111827;
}

.chunk__title {
  margin: 0;
  font-family: var(--ca-font-display, inherit);
  font-size: 12.5pt;
  font-weight: 800;
  line-height: 1.15;
  color: #111827;
}

.chunk__cont {
  font-size: 9pt;
  font-weight: 600;
  color: #4b5563;
}

.chunk__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 1.2mm;
  margin: 0.8mm 0 0;
  font-size: 8pt;
  color: #374151;
}

.list {
  width: 100%;
  table-layout: fixed;
  border-collapse: collapse;
  font-size: 8.5pt;
  line-height: 1.25;
}

.list th {
  padding: 1mm 1.5mm;
  border-bottom: 0.4mm solid #111827;
  text-align: left;
  font-size: 7pt;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: #374151;
}

.list td {
  padding: 1.1mm 1.5mm;
  border-bottom: 0.2mm solid #d1d5db;
  vertical-align: top;
  overflow-wrap: anywhere;
  color: #111827;
}

.list__role-row td {
  padding: 1mm 1.5mm;
  border-bottom: 0.4mm solid #9ca3af;
  background: #eef0f3;
  font-size: 7.5pt;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #1f2937;
}

.list__name-col {
  width: 52mm;
}

.list__check-col {
  width: 10mm;
  text-align: center;
}

.list__checkbox {
  display: inline-block;
  width: 4mm;
  height: 4mm;
  border: 0.4mm solid #111827;
  border-radius: 0.8mm;
}

.list__name {
  font-weight: 600;
}

.list__age-col {
  width: 14mm;
  white-space: nowrap;
}

.list__contact {
  font-size: 7.5pt;
  color: #1f2937;
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
    page-break-after: always;
  }

  .sheet:last-child {
    break-after: auto;
    page-break-after: auto;
  }
}
</style>
