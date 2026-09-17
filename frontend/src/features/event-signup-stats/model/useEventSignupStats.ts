import { computed, ref, type MaybeRefOrGetter } from 'vue'
import type { ChartData, ChartOptions } from 'chart.js'

import { useEventSignupStats as useEventSignupStatsQuery } from '@/entities/event'
import type { EventSignupStatsActivity } from '@/entities/event'
import { ASSIGNMENT_STATUS_IDS } from '@/shared/config'
import { i18n } from '@/shared/i18n'
import { barOptions, useChartTheme, wrapLabel } from '@/shared/lib'
import type { ChartPalette } from '@/shared/lib'

/** One activity/role row of the signup statistics table, with counts filled to zero. */
export interface SignupStatsTableRow {
  readonly activityId: string
  readonly activityTitle: string
  readonly roleId: string
  readonly roleName: string
  readonly requested: number
  readonly confirmed: number
  readonly denied: number
  readonly total: number
}

const STATUS_ORDER: {
  readonly id: string
  readonly labelKey: 'requested' | 'confirmed' | 'denied'
  readonly color: (palette: ChartPalette) => string
}[] = [
  { id: ASSIGNMENT_STATUS_IDS.requested, labelKey: 'requested', color: (p) => p.warning },
  { id: ASSIGNMENT_STATUS_IDS.confirmed, labelKey: 'confirmed', color: (p) => p.success },
  { id: ASSIGNMENT_STATUS_IDS.denied, labelKey: 'denied', color: (p) => p.danger },
]

function countFor(
  activity: EventSignupStatsActivity,
  statusId: string,
  roleId: string | null,
): number {
  return activity.cells
    .filter((cell) => cell.statusId === statusId && (roleId === null || cell.roleId === roleId))
    .reduce((sum, cell) => sum + cell.count, 0)
}

/**
 * Signup statistics for one event: a role filter (default "all roles") that narrows the stacked
 * bar chart, plus a full activity x role detail table and the event-wide totals. Cells the API
 * omits (zero count) and activities without signups are filled in before charting.
 */
export function useEventSignupStats(eventId: MaybeRefOrGetter<string>) {
  const query = useEventSignupStatsQuery(eventId)
  const { palette } = useChartTheme()
  const roleFilter = ref('')

  const stats = computed(() => query.data.value ?? null)

  const isEmpty = computed(() => (stats.value?.activities.length ?? 0) === 0)

  const roleOptions = computed(() => [
    { id: '', name: i18n.global.t('pages.eventDetail.stats.allRoles') },
    ...(stats.value?.roles ?? []),
  ])

  const chartData = computed<ChartData<'bar'>>(() => {
    const current = stats.value
    if (!current) return { labels: [], datasets: [] }
    const role = roleFilter.value || null
    return {
      labels: current.activities.map((activity) => wrapLabel(activity.title)),
      datasets: STATUS_ORDER.map(({ id, labelKey, color }) => ({
        label: i18n.global.t(`pages.eventDetail.stats.table.${labelKey}`),
        data: current.activities.map((activity) => countFor(activity, id, role)),
        backgroundColor: color(palette.value),
        borderColor: palette.value.surface,
        borderWidth: 1.5,
        borderRadius: 3,
        borderSkipped: false,
      })),
    }
  })

  const chartOptions = computed<ChartOptions<'bar'>>(() => barOptions(palette.value, true))

  const tableRows = computed<SignupStatsTableRow[]>(() => {
    const current = stats.value
    if (!current) return []
    const rows: SignupStatsTableRow[] = []
    for (const activity of current.activities) {
      for (const role of current.roles) {
        const requested = countFor(activity, ASSIGNMENT_STATUS_IDS.requested, role.id)
        const confirmed = countFor(activity, ASSIGNMENT_STATUS_IDS.confirmed, role.id)
        const denied = countFor(activity, ASSIGNMENT_STATUS_IDS.denied, role.id)
        rows.push({
          activityId: activity.id,
          activityTitle: activity.title,
          roleId: role.id,
          roleName: role.name,
          requested,
          confirmed,
          denied,
          total: requested + confirmed + denied,
        })
      }
    }
    return rows
  })

  const totals = computed(
    () => stats.value?.totals ?? { total: 0, requested: 0, confirmed: 0, denied: 0 },
  )

  return {
    isLoading: query.isLoading,
    isError: query.isError,
    isEmpty,
    roleFilter,
    roleOptions,
    chartData,
    chartOptions,
    tableRows,
    totals,
  }
}
