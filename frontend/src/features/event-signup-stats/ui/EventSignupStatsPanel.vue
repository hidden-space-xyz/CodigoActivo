<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import { BaseChart, ChartCard } from '@/shared/ui'
import { formatNumber } from '@/shared/lib'

import { useEventSignupStats } from '../model/useEventSignupStats'

const props = defineProps<{
  /** Event whose signup statistics are shown. The backend restricts the endpoint to admins/members. */
  eventId: string
}>()

const { t } = useI18n()
const {
  isLoading,
  isError,
  isEmpty,
  roleFilter,
  roleOptions,
  chartData,
  chartOptions,
  tableRows,
  totals,
} = useEventSignupStats(() => props.eventId)
</script>

<template>
  <div class="signup-stats">
    <p v-if="isLoading" class="signup-stats__state">{{ t('pages.eventDetail.stats.loading') }}</p>
    <p v-else-if="isError" class="signup-stats__state">
      {{ t('pages.eventDetail.stats.loadError') }}
    </p>
    <p v-else-if="isEmpty" class="signup-stats__state">{{ t('pages.eventDetail.stats.empty') }}</p>

    <template v-else>
      <ChartCard
        :title="t('pages.eventDetail.stats.title')"
        :subtitle="t('pages.eventDetail.stats.subtitle')"
      >
        <div class="signup-stats__filter">
          <label for="signup-stats-role">{{ t('pages.eventDetail.stats.roleFilter') }}</label>
          <el-select id="signup-stats-role" v-model="roleFilter" class="signup-stats__select">
            <el-option
              v-for="role in roleOptions"
              :key="role.id"
              :label="role.name"
              :value="role.id"
            />
          </el-select>
        </div>
        <div class="signup-stats__canvas">
          <BaseChart type="bar" :data="chartData" :options="chartOptions" />
        </div>
      </ChartCard>

      <table class="signup-stats__table">
        <thead>
          <tr>
            <th>{{ t('pages.eventDetail.stats.table.activity') }}</th>
            <th>{{ t('pages.eventDetail.stats.table.role') }}</th>
            <th>{{ t('pages.eventDetail.stats.table.requested') }}</th>
            <th>{{ t('pages.eventDetail.stats.table.confirmed') }}</th>
            <th>{{ t('pages.eventDetail.stats.table.denied') }}</th>
            <th>{{ t('pages.eventDetail.stats.table.total') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in tableRows" :key="`${row.activityId}-${row.roleId}`">
            <td>{{ row.activityTitle }}</td>
            <td>{{ row.roleName }}</td>
            <td>{{ formatNumber(row.requested) }}</td>
            <td>{{ formatNumber(row.confirmed) }}</td>
            <td>{{ formatNumber(row.denied) }}</td>
            <td>{{ formatNumber(row.total) }}</td>
          </tr>
        </tbody>
        <tfoot>
          <tr>
            <td colspan="2">{{ t('pages.eventDetail.stats.totalsRow') }}</td>
            <td>{{ formatNumber(totals.requested) }}</td>
            <td>{{ formatNumber(totals.confirmed) }}</td>
            <td>{{ formatNumber(totals.denied) }}</td>
            <td>{{ formatNumber(totals.total) }}</td>
          </tr>
        </tfoot>
      </table>
    </template>
  </div>
</template>

<style scoped>
.signup-stats {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.signup-stats__state {
  padding: 12px 0 24px;
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.signup-stats__filter {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 16px;
}

.signup-stats__filter label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.signup-stats__select {
  min-width: 200px;
}

.signup-stats__canvas {
  position: relative;
  height: 320px;
}

.signup-stats__table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13.5px;
}

.signup-stats__table th,
.signup-stats__table td {
  padding: 10px 12px;
  text-align: left;
  border-bottom: 1px solid var(--ca-border-soft);
}

.signup-stats__table th {
  color: var(--ca-text-muted);
  font-weight: 600;
}

.signup-stats__table tfoot td {
  font-weight: 700;
  color: var(--ca-text-bright);
  border-top: 2px solid var(--ca-border-strong);
  border-bottom: none;
}
</style>
