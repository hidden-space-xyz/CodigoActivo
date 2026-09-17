import type { ChartData, ChartOptions, TooltipItem } from 'chart.js'

import { genderLabel } from '@/entities/user'
import { i18n } from '@/shared/i18n'
import { formatBucketLabel, formatNumber, type ChartPalette } from '@/shared/lib'
import type {
  DashboardSliceResponse,
  DashboardTimeSeriesResponse,
} from '@/shared/api/generated/models'

interface SeriesStyle {
  label: string
  color: (palette: ChartPalette) => string
  soft: (palette: ChartPalette) => string
}

type StyleMap = Record<string, SeriesStyle>

/** Labels and palette colors for the `userGrowth` and `usersByType` series, keyed by user type. */
export const USER_TYPE_STYLE: StyleMap = {
  member: {
    label: i18n.global.t('pages.admin.dashboard.series.member'),
    color: (p) => p.orange,
    soft: (p) => p.orangeSoft,
  },
  sponsor: {
    label: i18n.global.t('pages.admin.dashboard.series.sponsor'),
    color: (p) => p.lime,
    soft: (p) => p.limeSoft,
  },
  participant: {
    label: i18n.global.t('pages.admin.dashboard.series.participant'),
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
}

/** Inscription series styles keyed by status: green confirmed, amber requested, red denied. */
export const INSCRIPTION_STATUS_STYLE: StyleMap = {
  confirmed: {
    label: i18n.global.t('pages.admin.dashboard.series.confirmed'),
    color: (p) => p.success,
    soft: (p) => p.successSoft,
  },
  requested: {
    label: i18n.global.t('pages.admin.dashboard.series.requested'),
    color: (p) => p.warning,
    soft: (p) => p.warningSoft,
  },
  denied: {
    label: i18n.global.t('pages.admin.dashboard.series.denied'),
    color: (p) => p.danger,
    soft: (p) => p.dangerSoft,
  },
}

/** Styles for the adults/minors slices of the audience composition doughnut. */
export const AUDIENCE_STYLE: StyleMap = {
  adults: {
    label: i18n.global.t('pages.admin.dashboard.series.adults'),
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
  minors: {
    label: i18n.global.t('pages.admin.dashboard.series.minors'),
    color: (p) => p.lime,
    soft: (p) => p.limeSoft,
  },
}

/** Participant gender slice styles, keyed by the API gender value (`Male`, `Female`, `Other`). */
export const GENDER_STYLE: StyleMap = {
  Male: {
    label: genderLabel('Male'),
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
  Female: {
    label: genderLabel('Female'),
    color: (p) => p.lime,
    soft: (p) => p.limeSoft,
  },
  Other: {
    label: genderLabel('Other'),
    color: (p) => p.orange,
    soft: (p) => p.orangeSoft,
  },
}

/** Styles for published announcements and resources in the content bar chart. */
export const CONTENT_STYLE: StyleMap = {
  announcements: {
    label: i18n.global.t('pages.admin.dashboard.series.announcements'),
    color: (p) => p.orange,
    soft: (p) => p.orangeSoft,
  },
  resources: {
    label: i18n.global.t('pages.admin.dashboard.series.resources'),
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
}

/** Styles for past versus upcoming events in the monthly events calendar chart. */
export const CALENDAR_STYLE: StyleMap = {
  past: {
    label: i18n.global.t('pages.admin.dashboard.series.past'),
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
  upcoming: {
    label: i18n.global.t('pages.admin.dashboard.series.upcoming'),
    color: (p) => p.orange,
    soft: (p) => p.orangeSoft,
  },
}

function axisLabels(
  series: DashboardTimeSeriesResponse | undefined,
  granularity: string,
): string[] {
  return (series?.buckets ?? []).map((bucket) => formatBucketLabel(bucket, granularity))
}

/** Whether any bucket of any series has a positive value; used to show the empty state instead. */
export function hasSeriesData(series: DashboardTimeSeriesResponse | undefined): boolean {
  return (series?.series ?? []).some((set) => (set.values ?? []).some((value) => value > 0))
}

/** Whether at least one slice has a positive count, i.e. the doughnut would draw something. */
export function hasSliceData(slices: DashboardSliceResponse[] | null | undefined): boolean {
  return (slices ?? []).some((slice) => (slice.count ?? 0) > 0)
}

/**
 * Builds filled, smoothed line datasets for a stacked area chart. Series without an entry in
 * `styleMap` fall back to their raw key and the dim text color.
 */
export function stackedAreaData(
  series: DashboardTimeSeriesResponse | undefined,
  granularity: string,
  palette: ChartPalette,
  styleMap: StyleMap,
): ChartData<'line'> {
  return {
    labels: axisLabels(series, granularity),
    datasets: (series?.series ?? []).map((set) => {
      const style = styleMap[set.key ?? '']
      const color = style?.color(palette) ?? palette.textDim
      return {
        label: style?.label ?? set.key ?? '',
        data: set.values ?? [],
        borderColor: color,
        backgroundColor: style?.soft(palette) ?? color,
        fill: true,
        tension: 0.35,
        borderWidth: 2,
        pointRadius: 0,
        pointHoverRadius: 4,
        pointBackgroundColor: color,
      }
    }),
  }
}

/**
 * Builds one bar dataset per series, with x labels formatted for `granularity`. Unstyled series
 * fall back to their raw key and the dim text color.
 */
export function barSeriesData(
  series: DashboardTimeSeriesResponse | undefined,
  granularity: string,
  palette: ChartPalette,
  styleMap: StyleMap,
): ChartData<'bar'> {
  return {
    labels: axisLabels(series, granularity),
    datasets: (series?.series ?? []).map((set) => {
      const style = styleMap[set.key ?? '']
      return {
        label: style?.label ?? set.key ?? '',
        data: set.values ?? [],
        backgroundColor: style?.color(palette) ?? palette.textDim,
        borderColor: palette.surface,
        borderWidth: 1.5,
        borderRadius: 3,
        borderSkipped: false,
      }
    }),
  }
}

/**
 * Builds a single-dataset doughnut, dropping zero-count slices. Label and color come from
 * `styleMap` when the key is known, otherwise from the slice's own label/color sent by the API.
 */
export function doughnutData(
  slices: DashboardSliceResponse[] | null | undefined,
  palette: ChartPalette,
  styleMap?: StyleMap,
): ChartData<'doughnut'> {
  const items = (slices ?? []).filter((slice) => (slice.count ?? 0) > 0)
  return {
    labels: items.map(
      (slice) => styleMap?.[slice.key ?? '']?.label ?? slice.label ?? slice.key ?? '',
    ),
    datasets: [
      {
        data: items.map((slice) => slice.count ?? 0),
        backgroundColor: items.map(
          (slice) => styleMap?.[slice.key ?? '']?.color(palette) ?? slice.color ?? palette.textDim,
        ),
        borderColor: palette.surface,
        borderWidth: 2,
        hoverOffset: 6,
      },
    ],
  }
}

/**
 * Splits text into at most `maxLines` lines of up to `maxPerLine` characters for chart axis labels,
 * appending an ellipsis when words are cut off or a single word is too long.
 */
export function wrapLabel(text: string, maxPerLine = 26, maxLines = 2): string[] {
  const words = text.split(/\s+/).filter(Boolean)
  if (words.length === 0) return ['']

  const lines: string[] = []
  let current = ''
  for (const word of words) {
    const candidate = current ? `${current} ${word}` : word
    if (candidate.length <= maxPerLine) {
      current = candidate
    } else {
      if (current) lines.push(current)
      current = word
      if (lines.length >= maxLines) break
    }
  }
  if (lines.length < maxLines && current) lines.push(current)

  const truncated = lines.join(' ').length < text.replace(/\s+/g, ' ').length
  if (truncated && lines.length > 0) {
    const last = lines[lines.length - 1] ?? ''
    lines[lines.length - 1] =
      `${last.length > maxPerLine - 1 ? last.slice(0, maxPerLine - 1) : last}…`
  }
  return lines.map((line) =>
    line.length > maxPerLine ? `${line.slice(0, maxPerLine - 1)}…` : line,
  )
}

/** Single-color ranking dataset of confirmed inscriptions; `labels` may be pre-wrapped lines. */
export function rankingBarData(
  labels: (string | string[])[],
  values: number[],
  color: string,
  palette: ChartPalette,
): ChartData<'bar'> {
  return {
    labels,
    datasets: [
      {
        label: i18n.global.t('pages.admin.dashboard.series.confirmedDataset'),
        data: values,
        backgroundColor: color,
        borderColor: palette.surface,
        borderWidth: 1.5,
        borderRadius: 4,
        borderSkipped: false,
      },
    ],
  }
}

function legend(palette: ChartPalette, display = true) {
  return {
    display,
    position: 'bottom' as const,
    labels: {
      color: palette.textMuted,
      usePointStyle: true,
      pointStyle: 'circle' as const,
      boxWidth: 8,
      boxHeight: 8,
      padding: 14,
      font: { size: 12 },
    },
  }
}

function tooltipBox(palette: ChartPalette) {
  return {
    backgroundColor: palette.surface,
    titleColor: palette.text,
    bodyColor: palette.textMuted,
    borderColor: palette.border,
    borderWidth: 1,
    padding: 10,
    cornerRadius: 8,
    usePointStyle: true,
  }
}

function valueScale(palette: ChartPalette, stacked: boolean) {
  return {
    stacked,
    beginAtZero: true,
    border: { display: false },
    grid: { color: palette.grid },
    ticks: { color: palette.textMuted, precision: 0, font: { size: 11 } },
  }
}

function categoryScale(palette: ChartPalette, stacked: boolean, { autoSkip = true } = {}) {
  return {
    stacked,
    border: { display: false },
    grid: { display: false },
    ticks: {
      color: palette.textMuted,
      font: { size: 11 },
      maxRotation: 0,
      autoSkip,
      autoSkipPadding: 12,
    },
  }
}

/** Options for the stacked area chart: index-mode tooltip over all series and bottom legend. */
export function areaOptions(palette: ChartPalette): ChartOptions<'line'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: legend(palette),
      tooltip: {
        ...tooltipBox(palette),
        callbacks: {
          label: (item: TooltipItem<'line'>) =>
            ` ${item.dataset.label ?? ''}: ${formatNumber(item.parsed.y)}`,
        },
      },
    },
    scales: { x: categoryScale(palette, true), y: valueScale(palette, true) },
  }
}

/** Bar chart options; `stacked` stacks both axes. Tooltips show formatted numbers per dataset. */
export function barOptions(palette: ChartPalette, stacked: boolean): ChartOptions<'bar'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: legend(palette),
      tooltip: {
        ...tooltipBox(palette),
        callbacks: {
          label: (item: TooltipItem<'bar'>) =>
            ` ${item.dataset.label ?? ''}: ${formatNumber(item.parsed.y)}`,
        },
      },
    },
    scales: { x: categoryScale(palette, stacked), y: valueScale(palette, stacked) },
  }
}

/**
 * Horizontal bar options without legend. `fullLabels` holds the unwrapped titles, shown in the
 * tooltip because the axis labels may be truncated by `wrapLabel`.
 */
export function rankingOptions(palette: ChartPalette, fullLabels: string[]): ChartOptions<'bar'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: 'y',
    plugins: {
      legend: legend(palette, false),
      tooltip: {
        ...tooltipBox(palette),
        callbacks: {
          title: (items: TooltipItem<'bar'>[]) => fullLabels[items[0]?.dataIndex ?? 0] ?? '',
          label: (item: TooltipItem<'bar'>) =>
            i18n.global.t('pages.admin.dashboard.tooltip.ranking', {
              n: formatNumber(item.parsed.x),
            }),
        },
      },
    },
    scales: {
      x: valueScale(palette, false),
      y: categoryScale(palette, false, { autoSkip: false }),
    },
  }
}

/** Doughnut options whose tooltip shows each slice with its percentage of the visible slices. */
export function doughnutOptions(palette: ChartPalette): ChartOptions<'doughnut'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '62%',
    plugins: {
      legend: legend(palette),
      tooltip: {
        ...tooltipBox(palette),
        callbacks: {
          label: (item: TooltipItem<'doughnut'>) => {
            const data = item.dataset.data
            const total = data.reduce(
              (sum, value, index) =>
                item.chart.getDataVisibility(index) ? sum + (value ?? 0) : sum,
              0,
            )
            const value = item.parsed
            const percent = total > 0 ? Math.round((value / total) * 100) : 0
            return i18n.global.t('pages.admin.dashboard.tooltip.slice', {
              label: item.label,
              n: formatNumber(value),
              p: percent,
            })
          },
        },
      },
    },
  }
}
