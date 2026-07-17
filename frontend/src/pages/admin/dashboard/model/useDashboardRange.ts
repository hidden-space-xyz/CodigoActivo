import { computed, ref } from 'vue'

import { i18n } from '@/shared/i18n'
import { toDateOnly } from '@/shared/lib'

export type RangePreset = '30d' | '90d' | '12m' | 'custom'

export interface RangeOption {
  value: RangePreset
  label: string
}

export const RANGE_OPTIONS: readonly RangeOption[] = [
  { value: '30d', label: i18n.global.t('pages.admin.dashboard.range.preset30d') },
  { value: '90d', label: i18n.global.t('pages.admin.dashboard.range.preset90d') },
  { value: '12m', label: i18n.global.t('pages.admin.dashboard.range.preset12m') },
]

function startOfToday(): Date {
  const date = new Date()
  date.setHours(0, 0, 0, 0)
  return date
}

function daysAgo(days: number): Date {
  const date = startOfToday()
  date.setDate(date.getDate() - days)
  return date
}

function monthsAgo(months: number): Date {
  const date = startOfToday()
  date.setMonth(date.getMonth() - months)
  return date
}

export function useDashboardRange() {
  const preset = ref<RangePreset>('12m')
  const customRange = ref<(Date | null)[] | null>(null)

  const range = computed<{ from: string; to: string }>(() => {
    const custom = customRange.value
    if (preset.value === 'custom' && custom?.[0] instanceof Date) {
      const end = custom[1] instanceof Date ? custom[1] : startOfToday()
      return { from: toDateOnly(custom[0]), to: toDateOnly(end) }
    }

    const from =
      preset.value === '30d' ? daysAgo(29) : preset.value === '90d' ? daysAgo(89) : monthsAgo(12)
    return { from: toDateOnly(from), to: toDateOnly(startOfToday()) }
  })

  function setPreset(value: RangePreset): void {
    preset.value = value
    if (value !== 'custom') customRange.value = null
  }

  function setCustomRange(value: (Date | null)[] | null): void {
    customRange.value = value
    preset.value = value?.[0] instanceof Date ? 'custom' : '12m'
  }

  return { preset, customRange, range, setPreset, setCustomRange }
}
