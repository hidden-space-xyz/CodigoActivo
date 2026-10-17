<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import type { TranslationKey } from '@/shared/i18n'
import { formatNumber } from '@/shared/lib/number'
import { ChartCard } from '@/shared/ui/chart'

import { seatPercent } from '../lib/seats'
import type { RoleCoverage } from '../model/types'
import SeatMeter from './SeatMeter.vue'

const props = defineProps<{
  /**
   * Seats per role across the upcoming activities, each activity counting up to its desired
   * number; a role no activity sets a desired number for shows no percentage.
   */
  roles: readonly RoleCoverage[]
}>()

const ROLE_LABELS: Record<string, TranslationKey> = {
  leader: 'pages.admin.dashboard.series.roles.leader',
  volunteer: 'pages.admin.dashboard.series.roles.volunteer',
  participant: 'pages.admin.dashboard.series.roles.participant',
}

const { t } = useI18n()

const rows = computed(() =>
  props.roles.map((role) => {
    const labelKey = ROLE_LABELS[role.key]
    return {
      ...role,
      label: labelKey ? t(labelKey) : role.key,
      percent: seatPercent(role.confirmed, role.desired),
    }
  }),
)
</script>

<template>
  <ChartCard
    :title="$t('pages.admin.dashboard.roleCoverage.title')"
    :subtitle="$t('pages.admin.dashboard.roleCoverage.subtitle')"
  >
    <ul class="coverage">
      <li v-for="role in rows" :key="role.key" class="coverage__role">
        <span class="coverage__name">{{ role.label }}</span>
        <SeatMeter class="coverage__meter" :percent="role.percent" />
        <span v-if="role.percent === null" class="coverage__figures coverage__figures--none">
          {{ $t('pages.admin.dashboard.roleCoverage.noCapacity') }}
        </span>
        <span v-else class="coverage__figures">
          <strong class="coverage__pct">{{ role.percent }}%</strong>
          {{
            $t('pages.admin.dashboard.roleCoverage.seats', {
              confirmed: formatNumber(role.confirmed),
              desired: formatNumber(role.desired),
            })
          }}
        </span>
      </li>
    </ul>
  </ChartCard>
</template>

<style scoped>
.coverage {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 18px;
  padding-top: 4px;
}

.coverage__role {
  display: grid;
  grid-template-columns: 110px minmax(0, 1fr) 140px;
  align-items: center;
  gap: 12px;
}

.coverage__name {
  font-size: 14px;
  color: var(--ca-text);
}

.coverage__figures {
  font-size: 12.5px;
  color: var(--ca-text-muted);
  text-align: right;
}

.coverage__figures--none {
  color: var(--ca-text-dim);
}

.coverage__pct {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-bright);
}

@media (max-width: 640px) {
  .coverage__role {
    grid-template-columns: minmax(0, 1fr) auto;
  }

  .coverage__meter {
    grid-column: 1 / -1;
    grid-row: 2;
  }
}
</style>
