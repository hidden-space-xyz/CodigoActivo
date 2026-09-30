import type { ChartData, ChartOptions, TooltipItem } from 'chart.js'

import { genderLabelKey } from '@/entities/user'
import type { Translate, TranslationKey } from '@/shared/i18n'
import {
  barOptions,
  categoryScale,
  type ChartPalette,
  legend,
  tooltipBox,
  valueScale,
  wrapLabel,
} from '@/shared/lib/chart'
import { formatNumber } from '@/shared/lib/number'

import type { Slice, TimeSeries } from '../model/types'
import { formatBucketLabel } from './bucket-label'

export { barOptions, wrapLabel }

interface SeriesStyle {
  labelKey: TranslationKey
  color: (palette: ChartPalette) => string
  soft: (palette: ChartPalette) => string
}

type StyleMap = Record<string, SeriesStyle>

/** Labels and palette colors for the `userGrowth` and `usersByType` series, keyed by user type. */
export const USER_TYPE_STYLE: StyleMap = {
  member: {
    labelKey: 'pages.admin.dashboard.series.member',
    color: (p) => p.orange,
    soft: (p) => p.orangeSoft,
  },
  sponsor: {
    labelKey: 'pages.admin.dashboard.series.sponsor',
    color: (p) => p.lime,
    soft: (p) => p.limeSoft,
  },
  participant: {
    labelKey: 'pages.admin.dashboard.series.participant',
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
}

/** Inscription series styles keyed by status: green confirmed, amber requested, red denied. */
export const INSCRIPTION_STATUS_STYLE: StyleMap = {
  confirmed: {
    labelKey: 'pages.admin.dashboard.series.confirmed',
    color: (p) => p.success,
    soft: (p) => p.successSoft,
  },
  requested: {
    labelKey: 'pages.admin.dashboard.series.requested',
    color: (p) => p.warning,
    soft: (p) => p.warningSoft,
  },
  denied: {
    labelKey: 'pages.admin.dashboard.series.denied',
    color: (p) => p.danger,
    soft: (p) => p.dangerSoft,
  },
}

/** Styles for the adults/minors slices of the audience composition doughnut. */
export const AUDIENCE_STYLE: StyleMap = {
  adults: {
    labelKey: 'pages.admin.dashboard.series.adults',
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
  minors: {
    labelKey: 'pages.admin.dashboard.series.minors',
    color: (p) => p.lime,
    soft: (p) => p.limeSoft,
  },
}

/**
 * Participant gender slice styles, keyed by the API gender value. `PreferNotToSay` is neutral
 * gray because it marks an undisclosed value rather than a category.
 */
export const GENDER_STYLE: StyleMap = {
  Male: {
    labelKey: genderLabelKey('Male'),
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
  Female: {
    labelKey: genderLabelKey('Female'),
    color: (p) => p.lime,
    soft: (p) => p.limeSoft,
  },
  Other: {
    labelKey: genderLabelKey('Other'),
    color: (p) => p.orange,
    soft: (p) => p.orangeSoft,
  },
  PreferNotToSay: {
    labelKey: genderLabelKey('PreferNotToSay'),
    color: (p) => p.textDim,
    soft: (p) => p.border,
  },
}

/** Styles for published news items and resources in the content bar chart. */
export const CONTENT_STYLE: StyleMap = {
  news: {
    labelKey: 'pages.admin.dashboard.series.news',
    color: (p) => p.orange,
    soft: (p) => p.orangeSoft,
  },
  resources: {
    labelKey: 'pages.admin.dashboard.series.resources',
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
}

/** Styles for past versus upcoming events in the monthly events calendar chart. */
export const CALENDAR_STYLE: StyleMap = {
  past: {
    labelKey: 'pages.admin.dashboard.series.past',
    color: (p) => p.azure,
    soft: (p) => p.azureSoft,
  },
  upcoming: {
    labelKey: 'pages.admin.dashboard.series.upcoming',
    color: (p) => p.orange,
    soft: (p) => p.orangeSoft,
  },
}

function axisLabels(series: TimeSeries | undefined, granularity: string): string[] {
  return (series?.buckets ?? []).map((bucket) => formatBucketLabel(bucket, granularity))
}

/** Whether any bucket of any series has a positive value; used to show the empty state instead. */
export function hasSeriesData(series: TimeSeries | undefined): boolean {
  return (series?.series ?? []).some((set) => set.values.some((value) => value > 0))
}

/** Whether at least one slice has a positive count, i.e. the doughnut would draw something. */
export function hasSliceData(slices: readonly Slice[] | undefined): boolean {
  return (slices ?? []).some((slice) => slice.count > 0)
}

/**
 * Builds filled, smoothed line datasets for a stacked area chart. Series without an entry in
 * `styleMap` fall back to their raw key and the dim text color.
 */
export function stackedAreaData(
  series: TimeSeries | undefined,
  granularity: string,
  palette: ChartPalette,
  styleMap: StyleMap,
  t: Translate,
): ChartData<'line'> {
  return {
    labels: axisLabels(series, granularity),
    datasets: (series?.series ?? []).map((set) => {
      const style = styleMap[set.key]
      const color = style?.color(palette) ?? palette.textDim
      return {
        label: style ? t(style.labelKey) : set.key,
        data: [...set.values],
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
  series: TimeSeries | undefined,
  granularity: string,
  palette: ChartPalette,
  styleMap: StyleMap,
  t: Translate,
): ChartData<'bar'> {
  return {
    labels: axisLabels(series, granularity),
    datasets: (series?.series ?? []).map((set) => {
      const style = styleMap[set.key]
      return {
        label: style ? t(style.labelKey) : set.key,
        data: [...set.values],
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
  slices: readonly Slice[] | undefined,
  palette: ChartPalette,
  t: Translate,
  styleMap?: StyleMap,
): ChartData<'doughnut'> {
  const items = (slices ?? []).filter((slice) => slice.count > 0)
  return {
    labels: items.map((slice) => {
      const style = styleMap?.[slice.key]
      return style ? t(style.labelKey) : (slice.label ?? slice.key)
    }),
    datasets: [
      {
        data: items.map((slice) => slice.count),
        backgroundColor: items.map(
          (slice) => styleMap?.[slice.key]?.color(palette) ?? slice.color ?? palette.textDim,
        ),
        borderColor: palette.surface,
        borderWidth: 2,
        hoverOffset: 6,
      },
    ],
  }
}

/** Single-color ranking dataset of confirmed inscriptions; `labels` may be pre-wrapped lines. */
export function rankingBarData(
  labels: (string | string[])[],
  values: number[],
  color: string,
  palette: ChartPalette,
  t: Translate,
): ChartData<'bar'> {
  return {
    labels,
    datasets: [
      {
        label: t('pages.admin.dashboard.series.confirmedDataset'),
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

/**
 * Horizontal bar options without legend. `fullLabels` holds the unwrapped titles, shown in the
 * tooltip because the axis labels may be truncated by `wrapLabel`.
 */
export function rankingOptions(
  palette: ChartPalette,
  fullLabels: string[],
  t: Translate,
): ChartOptions<'bar'> {
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
            t('pages.admin.dashboard.tooltip.ranking', { n: formatNumber(item.parsed.x) }),
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
export function doughnutOptions(palette: ChartPalette, t: Translate): ChartOptions<'doughnut'> {
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
            return t('pages.admin.dashboard.tooltip.slice', {
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
