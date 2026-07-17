<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { formatNumber, formatSignedPercent } from '@/shared/lib'
import type { DashboardKpiResponse } from '@/shared/api/generated/models'

const { t } = useI18n()

const props = defineProps<{ kpis: DashboardKpiResponse[] }>()

interface TileMeta {
  key: string
  label: string
  icon: string
  color: string
}

const TILES: readonly TileMeta[] = [
  {
    key: 'users',
    label: t('pages.admin.dashboard.kpi.users'),
    icon: 'pi pi-users',
    color: 'var(--ca-azure)',
  },
  {
    key: 'members',
    label: t('pages.admin.dashboard.kpi.members'),
    icon: 'pi pi-id-card',
    color: 'var(--ca-orange)',
  },
  {
    key: 'inscriptions',
    label: t('pages.admin.dashboard.kpi.inscriptions'),
    icon: 'pi pi-check-square',
    color: 'var(--ca-lime)',
  },
  {
    key: 'events',
    label: t('pages.admin.dashboard.kpi.events'),
    icon: 'pi pi-calendar',
    color: 'var(--ca-orange)',
  },
  {
    key: 'resources',
    label: t('pages.admin.dashboard.kpi.resources'),
    icon: 'pi pi-book',
    color: 'var(--ca-azure)',
  },
  {
    key: 'announcements',
    label: t('pages.admin.dashboard.kpi.announcements'),
    icon: 'pi pi-megaphone',
    color: 'var(--ca-lime)',
  },
]

const tiles = computed(() => {
  const byKey = new Map(props.kpis.map((kpi) => [kpi.key ?? '', kpi]))
  return TILES.map((meta) => {
    const kpi = byKey.get(meta.key)
    const total = kpi?.total ?? 0
    const inRange = kpi?.inRange ?? 0
    const previous = kpi?.previousRange ?? 0
    const trend = inRange === previous ? 'flat' : inRange > previous ? 'up' : 'down'
    const percent = previous > 0 ? ((inRange - previous) / previous) * 100 : null
    return { ...meta, total, inRange, trend, percent }
  })
})
</script>

<template>
  <div class="kpi-grid">
    <article
      v-for="tile in tiles"
      :key="tile.key"
      class="kpi-card"
      :style="{ '--accent': tile.color }"
    >
      <div class="kpi-card__top">
        <span class="kpi-card__icon"><i :class="tile.icon" /></span>
        <span
          v-if="tile.percent !== null"
          class="kpi-card__delta"
          :class="`kpi-card__delta--${tile.trend}`"
        >
          <i
            :class="
              tile.trend === 'up'
                ? 'pi pi-arrow-up-right'
                : tile.trend === 'down'
                  ? 'pi pi-arrow-down-right'
                  : 'pi pi-minus'
            "
          />
          {{ formatSignedPercent(tile.percent) }}
        </span>
      </div>
      <div class="kpi-card__value">{{ formatNumber(tile.total) }}</div>
      <div class="kpi-card__label">{{ tile.label }}</div>
      <div class="kpi-card__foot">
        {{
          tile.inRange > 0
            ? $t('pages.admin.dashboard.kpi.inRange', { n: formatNumber(tile.inRange) })
            : $t('pages.admin.dashboard.kpi.noNew')
        }}
      </div>
    </article>
  </div>
</template>

<style scoped>
.kpi-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(190px, 1fr));
  gap: 16px;
}

.kpi-card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-left: 3px solid var(--accent);
  border-radius: 16px;
  padding: 18px 20px;
}

.kpi-card__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.kpi-card__icon {
  color: var(--accent);
  font-size: 20px;
}

.kpi-card__delta {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 12px;
  font-weight: 600;
  font-family: var(--ca-font-mono);
  padding: 2px 8px;
  border-radius: 999px;
}

.kpi-card__delta--up {
  color: var(--ca-success-ink);
  background: var(--ca-success-soft);
}

.kpi-card__delta--down {
  color: var(--ca-danger-ink);
  background: var(--ca-danger-soft);
}

.kpi-card__delta--flat {
  color: var(--ca-text-muted);
  background: var(--ca-surface-2);
}

.kpi-card__value {
  margin-top: 12px;
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 32px;
  color: var(--ca-text-bright);
}

.kpi-card__label {
  margin-top: 2px;
  font-size: 14px;
  color: var(--ca-text-muted);
}

.kpi-card__foot {
  margin-top: 10px;
  font-size: 12px;
  color: var(--ca-text-dim);
}
</style>
