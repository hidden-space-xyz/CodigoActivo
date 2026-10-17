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
import { formatNumber, formatScore } from '@/shared/lib/number'

import type { Breakdown, Satisfaction, Slice, TimeSeries, TopEvent } from '../model/types'
import { formatBucketLabel } from './bucket-label'
import { seatPercent } from './seats'

interface SeriesStyle {
  labelKey: TranslationKey
  color: (palette: ChartPalette) => string
}

type StyleMap = Record<string, SeriesStyle>

type LabelMap = Record<string, TranslationKey>

/** Chart.js data and options of one dashboard card, and whether it has nothing to draw. */
export interface ChartView {
  readonly data: ChartData<'bar'>
  readonly options: ChartOptions<'bar'>
  readonly empty: boolean
}

/** Labels and palette colors of the new users series, keyed by user type. */
export const USER_TYPE_STYLE: StyleMap = {
  member: { labelKey: 'pages.admin.dashboard.series.member', color: (p) => p.orange },
  sponsor: { labelKey: 'pages.admin.dashboard.series.sponsor', color: (p) => p.lime },
  participant: { labelKey: 'pages.admin.dashboard.series.participant', color: (p) => p.azure },
}

/** Inscription series styles keyed by status: green confirmed, amber requested, red denied. */
export const INSCRIPTION_STATUS_STYLE: StyleMap = {
  confirmed: { labelKey: 'pages.admin.dashboard.series.confirmed', color: (p) => p.success },
  requested: { labelKey: 'pages.admin.dashboard.series.requested', color: (p) => p.warning },
  denied: { labelKey: 'pages.admin.dashboard.series.denied', color: (p) => p.danger },
}

/**
 * Participant gender series styles, keyed by the API gender value. `PreferNotToSay` is neutral
 * gray because it marks an undisclosed value rather than a category.
 */
export const GENDER_STYLE: StyleMap = {
  Male: { labelKey: genderLabelKey('Male'), color: (p) => p.azure },
  Female: { labelKey: genderLabelKey('Female'), color: (p) => p.lime },
  Other: { labelKey: genderLabelKey('Other'), color: (p) => p.orange },
  PreferNotToSay: { labelKey: genderLabelKey('PreferNotToSay'), color: (p) => p.textDim },
}

/** Labels of the participant profile age groups, keyed by the API group. */
const AGE_GROUP_LABELS: LabelMap = {
  '0-5': 'pages.admin.dashboard.series.ageGroups.upTo5',
  '6-11': 'pages.admin.dashboard.series.ageGroups.from6To11',
  '12-17': 'pages.admin.dashboard.series.ageGroups.from12To17',
  adults: 'pages.admin.dashboard.series.ageGroups.adults',
}

/** Labels of the recurrence groups, keyed by how many events the API counted. */
const RECURRENCE_LABELS: LabelMap = {
  '1': 'pages.admin.dashboard.series.recurrence.once',
  '2': 'pages.admin.dashboard.series.recurrence.twice',
  '3-5': 'pages.admin.dashboard.series.recurrence.threeToFive',
  '6+': 'pages.admin.dashboard.series.recurrence.sixOrMore',
}

function label(key: string, labels: LabelMap, t: Translate): string {
  const labelKey = labels[key]
  return labelKey ? t(labelKey) : key
}

function hasSeriesData(chart: TimeSeries | Breakdown): boolean {
  return chart.series.some((set) => set.values.some((value) => value > 0))
}

function styledDatasets(
  chart: TimeSeries | Breakdown,
  palette: ChartPalette,
  styleMap: StyleMap,
  t: Translate,
): ChartData<'bar'>['datasets'] {
  return chart.series.map((set) => {
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
  })
}

function rankingData(
  labels: (string | string[])[],
  values: number[],
  colors: string | string[],
  datasetLabel: string,
  palette: ChartPalette,
): ChartData<'bar'> {
  return {
    labels,
    datasets: [
      {
        label: datasetLabel,
        data: values,
        backgroundColor: colors,
        borderColor: palette.surface,
        borderWidth: 1.5,
        borderRadius: 4,
        borderSkipped: false,
      },
    ],
  }
}

/**
 * Horizontal single-series bars without legend. `fullLabels` holds the unwrapped names shown as
 * tooltip titles, `describe` the tooltip lines of the bar at an index, and `max` caps the axis.
 */
function rankingOptions(
  palette: ChartPalette,
  fullLabels: string[],
  describe: (index: number) => string | string[],
  max?: number,
): ChartOptions<'bar'> {
  const x = valueScale(palette, false)
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
          label: (item: TooltipItem<'bar'>) => describe(item.dataIndex),
        },
      },
    },
    scales: {
      x: max === undefined ? x : { ...x, max },
      y: categoryScale(palette, false, { autoSkip: false }),
    },
  }
}

/**
 * Stacked bars per date bucket, one series per key styled by `styleMap` (unknown keys keep their
 * raw key and the dim color); empty when no bucket has a positive value.
 */
export function timeSeriesChart(
  series: TimeSeries,
  granularity: string,
  palette: ChartPalette,
  styleMap: StyleMap,
  t: Translate,
): ChartView {
  return {
    data: {
      labels: series.buckets.map((bucket) => formatBucketLabel(bucket, granularity)),
      datasets: styledDatasets(series, palette, styleMap, t),
    },
    options: barOptions(palette, true),
    empty: !hasSeriesData(series),
  }
}

/**
 * Ranking of confirmed signups per event. When the event's activities set a desired number, the
 * label keeps one line of the title and adds the share of seats filled; the tooltip shows the full
 * title and repeats the share with the seat counts.
 */
export function topEventsChart(
  events: readonly TopEvent[],
  palette: ChartPalette,
  t: Translate,
): ChartView {
  const percents = events.map((event) => seatPercent(event.seatsFilled, event.seatsDesired))
  return {
    data: rankingData(
      events.map((event, index) => {
        const percent = percents[index]
        return percent === null
          ? wrapLabel(event.title)
          : [
              ...wrapLabel(event.title, 26, 1),
              t('pages.admin.dashboard.chart.seatsCovered', { p: percent }),
            ]
      }),
      events.map((event) => event.confirmed),
      palette.orange,
      t('pages.admin.dashboard.series.confirmedDataset'),
      palette,
    ),
    options: rankingOptions(
      palette,
      events.map((event) => event.title),
      (index) => {
        const event = events[index]
        if (!event) return ''
        const confirmed = t('pages.admin.dashboard.tooltip.ranking', {
          n: formatNumber(event.confirmed),
        })
        const percent = percents[index]
        return percent === null
          ? confirmed
          : [
              confirmed,
              t('pages.admin.dashboard.tooltip.seats', {
                p: percent,
                filled: formatNumber(event.seatsFilled),
                desired: formatNumber(event.seatsDesired),
              }),
            ]
      },
    ),
    empty: events.length === 0,
  }
}

/** Ranking of confirmed signups per event category, each bar in its category's color. */
export function categoryDemandChart(
  categories: readonly Slice[],
  palette: ChartPalette,
  t: Translate,
): ChartView {
  const names = categories.map((category) => category.label ?? category.key)
  return {
    data: rankingData(
      names.map((name) => wrapLabel(name)),
      categories.map((category) => category.count),
      categories.map((category) => category.color ?? palette.textDim),
      t('pages.admin.dashboard.series.confirmedDataset'),
      palette,
    ),
    options: rankingOptions(palette, names, (index) =>
      t('pages.admin.dashboard.tooltip.ranking', {
        n: formatNumber(categories[index]?.count),
      }),
    ),
    empty: !categories.some((category) => category.count > 0),
  }
}

/** People per age group, one stacked segment per gender. */
export function participantProfileChart(
  profile: Breakdown,
  palette: ChartPalette,
  t: Translate,
): ChartView {
  return {
    data: {
      labels: profile.groups.map((group) => label(group, AGE_GROUP_LABELS, t)),
      datasets: styledDatasets(profile, palette, GENDER_STYLE, t),
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      indexAxis: 'y',
      plugins: {
        legend: legend(palette),
        tooltip: {
          ...tooltipBox(palette),
          callbacks: {
            label: (item: TooltipItem<'bar'>) =>
              ` ${item.dataset.label ?? ''}: ${formatNumber(item.parsed.x)}`,
          },
        },
      },
      scales: {
        x: valueScale(palette, true),
        y: categoryScale(palette, true, { autoSkip: false }),
      },
    },
    empty: !hasSeriesData(profile),
  }
}

/** People per number of events they joined, in the API's fixed order. */
export function recurrenceChart(
  groups: readonly Slice[],
  palette: ChartPalette,
  t: Translate,
): ChartView {
  const names = groups.map((group) => label(group.key, RECURRENCE_LABELS, t))
  return {
    data: rankingData(
      names,
      groups.map((group) => group.count),
      palette.lime,
      t('pages.admin.dashboard.series.people'),
      palette,
    ),
    options: rankingOptions(palette, names, (index) =>
      t('pages.admin.dashboard.tooltip.people', { n: formatNumber(groups[index]?.count) }),
    ),
    empty: !groups.some((group) => group.count > 0),
  }
}

/** Mean score per event on a 0–5 axis; the tooltip adds how many scored ratings it covers. */
export function satisfactionChart(
  satisfaction: Satisfaction,
  palette: ChartPalette,
  t: Translate,
): ChartView {
  const events = satisfaction.events
  return {
    data: rankingData(
      events.map((event) => wrapLabel(event.title)),
      events.map((event) => event.average),
      palette.azure,
      t('pages.admin.dashboard.series.averageScore'),
      palette,
    ),
    options: rankingOptions(
      palette,
      events.map((event) => event.title),
      (index) =>
        t('pages.admin.dashboard.tooltip.score', {
          score: formatScore(events[index]?.average),
          n: formatNumber(events[index]?.scored),
        }),
      5,
    ),
    empty: events.length === 0,
  }
}
