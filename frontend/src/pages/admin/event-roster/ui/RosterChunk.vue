<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import { fullName } from '@/entities/user'
import { ageFrom, formatDateTimeRange } from '@/shared/lib/date'

import {
  contactLines,
  guardianContactLines,
  roleHeadingKey,
  type RosterRow,
} from '../lib/roster-rows'
import type { RosterActivity, RosterParticipant } from '../model/types'

defineProps<{
  /** Activity whose table is printed. */
  activity: RosterActivity
  /** Rows of the table printed here, a part of the activity's rows or all of them. */
  rows: readonly RosterRow[]
  /** The table continues from a previous sheet; its title says so. */
  continued: boolean
}>()

const { t } = useI18n()

function ageLabel(participant: RosterParticipant): string {
  const age = ageFrom(participant.birthDate)
  return age === null ? '—' : t('pages.admin.eventRoster.ageYears', { age })
}

function participantsLabel(count: number): string {
  return t('pages.admin.eventRoster.participantsCount', { count }, count)
}
</script>

<template>
  <section class="chunk">
    <header class="chunk__head" data-part="head">
      <h2 class="chunk__title">
        {{ activity.title
        }}<span v-if="continued" class="chunk__cont">{{
          $t('pages.admin.eventRoster.continued')
        }}</span>
      </h2>
      <p class="chunk__meta">
        <span>{{ formatDateTimeRange(activity.startsAt, activity.endsAt) }}</span>
        <span>· {{ activity.location }}</span>
        <span>· {{ participantsLabel(activity.participants.length) }}</span>
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
        <template v-for="row in rows" :key="row.key">
          <tr v-if="row.kind === 'role'" class="list__role-row">
            <td colspan="5">{{ $t(roleHeadingKey(row.roleName), { name: row.roleName }) }}</td>
          </tr>
          <tr v-else>
            <td class="list__check-col"><span class="list__checkbox" /></td>
            <td class="list__name">{{ fullName(row.participant) }}</td>
            <td class="list__age-col">{{ ageLabel(row.participant) }}</td>
            <td class="list__contact">
              <template v-if="contactLines(row.participant).length">
                <div v-for="line in contactLines(row.participant)" :key="line">{{ line }}</div>
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
</template>

<style scoped>
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
</style>
