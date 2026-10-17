import { describe, expect, it } from 'vitest'

import {
  categoryDemandChart,
  GENDER_STYLE,
  INSCRIPTION_STATUS_STYLE,
  participantProfileChart,
  recurrenceChart,
  satisfactionChart,
  timeSeriesChart,
  topEventsChart,
  USER_TYPE_STYLE,
} from '@/pages/admin/dashboard/lib/charts'
import { formatBucketLabel } from '@/pages/admin/dashboard/lib/bucket-label'
import type { Breakdown, Slice, TimeSeries, TopEvent } from '@/pages/admin/dashboard/model/types'

import { t } from '../../../../../support/render'
import { TEST_PALETTE } from '../../../../../support/chart-mock'

interface TooltipCallbacks {
  title: (items: unknown[]) => string
  label: (item: unknown) => string | string[]
}

interface TestableOptions {
  indexAxis?: string
  plugins: {
    legend: { display: boolean }
    tooltip: { callbacks: TooltipCallbacks }
  }
  scales: Record<string, { stacked: boolean; max?: number; ticks: { autoSkip?: boolean } }>
}

function inspect(options: object): TestableOptions {
  return options as TestableOptions
}

function slice(
  key: string,
  count: number,
  label: string | null = null,
  color: string | null = null,
): Slice {
  return { key, label, color, count }
}

function topEvent(overrides: Partial<TopEvent> = {}): TopEvent {
  return {
    id: 'event-1',
    title: 'Hackathon de primavera',
    confirmed: 42,
    seatsFilled: 30,
    seatsDesired: 40,
    ...overrides,
  }
}

describe('dashboard chart styles', () => {
  it('resolve labels and colors from the palette', () => {
    expect(USER_TYPE_STYLE.member?.labelKey).toBe('pages.admin.dashboard.series.member')
    expect(USER_TYPE_STYLE.member?.color(TEST_PALETTE)).toBe('orange')
    expect(USER_TYPE_STYLE.sponsor?.color(TEST_PALETTE)).toBe('lime')
    expect(USER_TYPE_STYLE.participant?.color(TEST_PALETTE)).toBe('azure')

    expect(INSCRIPTION_STATUS_STYLE.confirmed?.color(TEST_PALETTE)).toBe('success')
    expect(INSCRIPTION_STATUS_STYLE.requested?.color(TEST_PALETTE)).toBe('warning')
    expect(INSCRIPTION_STATUS_STYLE.denied?.color(TEST_PALETTE)).toBe('danger')

    expect(GENDER_STYLE.Male?.color(TEST_PALETTE)).toBe('azure')
    expect(GENDER_STYLE.Female?.color(TEST_PALETTE)).toBe('lime')
    expect(GENDER_STYLE.Other?.color(TEST_PALETTE)).toBe('orange')
    expect(GENDER_STYLE.PreferNotToSay?.labelKey).toBe('entities.user.gender.PreferNotToSay')
    expect(GENDER_STYLE.PreferNotToSay?.color(TEST_PALETTE)).toBe('text-dim')
  })
})

describe('timeSeriesChart', () => {
  const series: TimeSeries = {
    buckets: ['2026-08-01', '2026-09-01'],
    series: [
      { key: 'member', values: [1, 2] },
      { key: 'unknown', values: [0, 0] },
    ],
  }

  it('stacks one styled bar dataset per series over the formatted buckets', () => {
    const chart = timeSeriesChart(series, 'month', TEST_PALETTE, USER_TYPE_STYLE, t)

    expect(chart.empty).toBe(false)
    expect(chart.data.labels).toEqual([
      formatBucketLabel('2026-08-01', 'month'),
      formatBucketLabel('2026-09-01', 'month'),
    ])
    expect(chart.data.datasets[0]).toMatchObject({
      label: t('pages.admin.dashboard.series.member'),
      data: [1, 2],
      backgroundColor: 'orange',
      borderColor: 'surface',
      borderRadius: 3,
      borderSkipped: false,
    })
    expect(chart.data.datasets[1]).toMatchObject({ label: 'unknown', backgroundColor: 'text-dim' })
    expect(inspect(chart.options).scales.x?.stacked).toBe(true)
  })

  it('is empty when no bucket has a positive value', () => {
    const chart = timeSeriesChart(
      { buckets: ['2026-08-01'], series: [{ key: 'member', values: [0] }] },
      'day',
      TEST_PALETTE,
      USER_TYPE_STYLE,
      t,
    )

    expect(chart.empty).toBe(true)
    expect(chart.data.labels).toEqual([formatBucketLabel('2026-08-01', 'day')])
  })
})

describe('topEventsChart', () => {
  it('ranks confirmed signups and adds the share of seats filled to the label', () => {
    const chart = topEventsChart(
      [topEvent(), topEvent({ id: 'event-2', title: 'Taller', confirmed: 5, seatsDesired: 0 })],
      TEST_PALETTE,
      t,
    )

    expect(chart.empty).toBe(false)
    expect(chart.data.labels).toEqual([
      ['Hackathon de primavera', t('pages.admin.dashboard.chart.seatsCovered', { p: 75 })],
      ['Taller'],
    ])
    expect(chart.data.datasets).toEqual([
      expect.objectContaining({
        label: t('pages.admin.dashboard.series.confirmedDataset'),
        data: [42, 5],
        backgroundColor: 'orange',
      }),
    ])
  })

  it('shows the full title, the confirmed count and the seats in the tooltip', () => {
    const chart = topEventsChart(
      [topEvent(), topEvent({ id: 'event-2', title: 'Taller', confirmed: 5, seatsDesired: 0 })],
      TEST_PALETTE,
      t,
    )
    const options = inspect(chart.options)
    const { title, label } = options.plugins.tooltip.callbacks

    expect(options.indexAxis).toBe('y')
    expect(options.plugins.legend.display).toBe(false)
    expect(options.scales.y?.ticks.autoSkip).toBe(false)
    expect(title([{ dataIndex: 1 }])).toBe('Taller')
    expect(title([])).toBe('Hackathon de primavera')
    expect(title([{ dataIndex: 5 }])).toBe('')
    expect(label({ dataIndex: 0 })).toEqual([
      t('pages.admin.dashboard.tooltip.ranking', { n: '42' }),
      t('pages.admin.dashboard.tooltip.seats', { p: 75, filled: '30', desired: '40' }),
    ])
    expect(label({ dataIndex: 1 })).toBe(t('pages.admin.dashboard.tooltip.ranking', { n: '5' }))
    expect(label({ dataIndex: 9 })).toBe('')
  })

  it('is empty without events', () => {
    expect(topEventsChart([], TEST_PALETTE, t).empty).toBe(true)
  })
})

describe('categoryDemandChart', () => {
  it('colors each bar with its category and falls back to the key and the dim color', () => {
    const chart = categoryDemandChart(
      [slice('cat-1', 3, 'Robótica', '#00ff00'), slice('cat-2', 1)],
      TEST_PALETTE,
      t,
    )

    expect(chart.empty).toBe(false)
    expect(chart.data.labels).toEqual([['Robótica'], ['cat-2']])
    expect(chart.data.datasets[0]).toMatchObject({
      data: [3, 1],
      backgroundColor: ['#00ff00', 'text-dim'],
    })
    const { title, label } = inspect(chart.options).plugins.tooltip.callbacks
    expect(title([{ dataIndex: 0 }])).toBe('Robótica')
    expect(label({ dataIndex: 0 })).toBe(t('pages.admin.dashboard.tooltip.ranking', { n: '3' }))
  })

  it('is empty when no category has a confirmed signup', () => {
    expect(categoryDemandChart([slice('cat-1', 0, 'Robótica')], TEST_PALETTE, t).empty).toBe(true)
  })
})

describe('participantProfileChart', () => {
  const profile: Breakdown = {
    groups: ['0-5', 'adults', 'unknown'],
    series: [
      { key: 'Male', values: [1, 0, 0] },
      { key: 'Female', values: [0, 4, 0] },
    ],
  }

  it('stacks one gender segment per age group on horizontal bars', () => {
    const chart = participantProfileChart(profile, TEST_PALETTE, t)
    const options = inspect(chart.options)

    expect(chart.empty).toBe(false)
    expect(chart.data.labels).toEqual([
      t('pages.admin.dashboard.series.ageGroups.upTo5'),
      t('pages.admin.dashboard.series.ageGroups.adults'),
      'unknown',
    ])
    expect(chart.data.datasets.map((dataset) => dataset.backgroundColor)).toEqual(['azure', 'lime'])
    expect(options.indexAxis).toBe('y')
    expect(options.plugins.legend.display).toBe(true)
    expect(options.scales.x?.stacked).toBe(true)
    expect(options.scales.y).toMatchObject({ stacked: true, ticks: { autoSkip: false } })
  })

  it('shows each gender with its count in the tooltip', () => {
    const { label } = inspect(participantProfileChart(profile, TEST_PALETTE, t).options).plugins
      .tooltip.callbacks

    expect(label({ dataset: { label: 'Mujer' }, parsed: { x: 1200 } })).toBe(' Mujer: 1200')
    expect(label({ dataset: {}, parsed: { x: 0 } })).toBe(' : 0')
  })

  it('is empty when every group is zero', () => {
    expect(
      participantProfileChart(
        { groups: ['adults'], series: [{ key: 'Male', values: [0] }] },
        TEST_PALETTE,
        t,
      ).empty,
    ).toBe(true)
  })
})

describe('recurrenceChart', () => {
  it('labels the groups by how many events people joined', () => {
    const chart = recurrenceChart(
      [slice('1', 7), slice('2', 3), slice('3-5', 1), slice('6+', 0), slice('other', 2)],
      TEST_PALETTE,
      t,
    )

    expect(chart.empty).toBe(false)
    expect(chart.data.labels).toEqual([
      t('pages.admin.dashboard.series.recurrence.once'),
      t('pages.admin.dashboard.series.recurrence.twice'),
      t('pages.admin.dashboard.series.recurrence.threeToFive'),
      t('pages.admin.dashboard.series.recurrence.sixOrMore'),
      'other',
    ])
    expect(chart.data.datasets[0]).toMatchObject({
      label: t('pages.admin.dashboard.series.people'),
      data: [7, 3, 1, 0, 2],
      backgroundColor: 'lime',
    })
    const { label } = inspect(chart.options).plugins.tooltip.callbacks
    expect(label({ dataIndex: 0 })).toBe(t('pages.admin.dashboard.tooltip.people', { n: '7' }))
  })

  it('is empty when nobody joined', () => {
    expect(recurrenceChart([slice('1', 0)], TEST_PALETTE, t).empty).toBe(true)
  })
})

describe('satisfactionChart', () => {
  it('draws the mean score per event on an axis capped at five', () => {
    const chart = satisfactionChart(
      {
        average: 4.2,
        scored: 12,
        events: [{ id: 'event-1', title: 'Hackathon', average: 4.25, scored: 8 }],
      },
      TEST_PALETTE,
      t,
    )
    const options = inspect(chart.options)

    expect(chart.empty).toBe(false)
    expect(chart.data.labels).toEqual([['Hackathon']])
    expect(chart.data.datasets[0]).toMatchObject({
      label: t('pages.admin.dashboard.series.averageScore'),
      data: [4.25],
      backgroundColor: 'azure',
    })
    expect(options.scales.x?.max).toBe(5)
    expect(options.plugins.tooltip.callbacks.label({ dataIndex: 0 })).toBe(
      t('pages.admin.dashboard.tooltip.score', { score: '4,3', n: '8' }),
    )
  })

  it('is empty without scored events', () => {
    expect(satisfactionChart({ average: null, scored: 0, events: [] }, TEST_PALETTE, t).empty).toBe(
      true,
    )
  })
})
