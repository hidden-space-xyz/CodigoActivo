import { describe, expect, it } from 'vitest'

import {
  areaOptions,
  AUDIENCE_STYLE,
  barOptions,
  barSeriesData,
  CALENDAR_STYLE,
  CONTENT_STYLE,
  doughnutData,
  doughnutOptions,
  GENDER_STYLE,
  hasSeriesData,
  hasSliceData,
  INSCRIPTION_STATUS_STYLE,
  rankingBarData,
  rankingOptions,
  stackedAreaData,
  USER_TYPE_STYLE,
  wrapLabel,
} from '@/pages/admin/dashboard/model/charts'
import { formatBucketLabel } from '@/shared/lib'
import type { DashboardTimeSeriesResponse } from '@/shared/api/generated/models'

import { TEST_PALETTE } from '../../../../support/fixtures/public-dashboard/builders'
import { t } from '../../../../support/render'

interface TooltipCallbacks {
  title: (items: unknown[]) => string
  label: (item: unknown) => string
}

interface TestableOptions {
  plugins: {
    legend: { display: boolean; labels: { color: string } }
    tooltip: { backgroundColor: string; callbacks: TooltipCallbacks }
  }
  scales: Record<string, { stacked: boolean; ticks: { autoSkip?: boolean; color: string } }>
}

function inspect(options: object): TestableOptions {
  return options as TestableOptions
}

const series: DashboardTimeSeriesResponse = {
  buckets: ['2026-08-01', '2026-09-01'],
  series: [{ key: 'member', values: [1, 2] }, { key: 'unknown', values: [3, 4] }, { values: null }],
}

describe('dashboard chart styles', () => {
  it('resolve labels and colors from the palette', () => {
    expect(USER_TYPE_STYLE.member?.label).toBe(t('pages.admin.dashboard.series.member'))
    expect(USER_TYPE_STYLE.member?.color(TEST_PALETTE)).toBe('orange')
    expect(USER_TYPE_STYLE.member?.soft(TEST_PALETTE)).toBe('orange-soft')
    expect(USER_TYPE_STYLE.sponsor?.color(TEST_PALETTE)).toBe('lime')
    expect(USER_TYPE_STYLE.sponsor?.soft(TEST_PALETTE)).toBe('lime-soft')
    expect(USER_TYPE_STYLE.participant?.color(TEST_PALETTE)).toBe('azure')
    expect(USER_TYPE_STYLE.participant?.soft(TEST_PALETTE)).toBe('azure-soft')

    expect(INSCRIPTION_STATUS_STYLE.confirmed?.color(TEST_PALETTE)).toBe('success')
    expect(INSCRIPTION_STATUS_STYLE.confirmed?.soft(TEST_PALETTE)).toBe('success-soft')
    expect(INSCRIPTION_STATUS_STYLE.requested?.color(TEST_PALETTE)).toBe('warning')
    expect(INSCRIPTION_STATUS_STYLE.requested?.soft(TEST_PALETTE)).toBe('warning-soft')
    expect(INSCRIPTION_STATUS_STYLE.denied?.color(TEST_PALETTE)).toBe('danger')
    expect(INSCRIPTION_STATUS_STYLE.denied?.soft(TEST_PALETTE)).toBe('danger-soft')

    expect(AUDIENCE_STYLE.adults?.color(TEST_PALETTE)).toBe('azure')
    expect(AUDIENCE_STYLE.adults?.soft(TEST_PALETTE)).toBe('azure-soft')
    expect(AUDIENCE_STYLE.minors?.color(TEST_PALETTE)).toBe('lime')
    expect(AUDIENCE_STYLE.minors?.soft(TEST_PALETTE)).toBe('lime-soft')

    expect(GENDER_STYLE.Male?.color(TEST_PALETTE)).toBe('azure')
    expect(GENDER_STYLE.Male?.soft(TEST_PALETTE)).toBe('azure-soft')
    expect(GENDER_STYLE.Female?.color(TEST_PALETTE)).toBe('lime')
    expect(GENDER_STYLE.Female?.soft(TEST_PALETTE)).toBe('lime-soft')
    expect(GENDER_STYLE.Other?.color(TEST_PALETTE)).toBe('orange')
    expect(GENDER_STYLE.Other?.soft(TEST_PALETTE)).toBe('orange-soft')
    expect(GENDER_STYLE.PreferNotToSay?.label).toBe(t('entities.user.gender.PreferNotToSay'))
    expect(GENDER_STYLE.PreferNotToSay?.color(TEST_PALETTE)).toBe('text-dim')
    expect(GENDER_STYLE.PreferNotToSay?.soft(TEST_PALETTE)).toBe('border')

    expect(CONTENT_STYLE.news?.color(TEST_PALETTE)).toBe('orange')
    expect(CONTENT_STYLE.news?.soft(TEST_PALETTE)).toBe('orange-soft')
    expect(CONTENT_STYLE.resources?.color(TEST_PALETTE)).toBe('azure')
    expect(CONTENT_STYLE.resources?.soft(TEST_PALETTE)).toBe('azure-soft')

    expect(CALENDAR_STYLE.past?.color(TEST_PALETTE)).toBe('azure')
    expect(CALENDAR_STYLE.past?.soft(TEST_PALETTE)).toBe('azure-soft')
    expect(CALENDAR_STYLE.upcoming?.color(TEST_PALETTE)).toBe('orange')
    expect(CALENDAR_STYLE.upcoming?.soft(TEST_PALETTE)).toBe('orange-soft')
  })
})

describe('hasSeriesData', () => {
  it('is false without series or with only zero values', () => {
    expect(hasSeriesData(undefined)).toBe(false)
    expect(hasSeriesData({})).toBe(false)
    expect(hasSeriesData({ series: [{ key: 'a', values: [0, 0] }, { key: 'b' }] })).toBe(false)
  })

  it('is true when any bucket is positive', () => {
    expect(hasSeriesData({ series: [{ values: [0] }, { values: [0, 2] }] })).toBe(true)
  })
})

describe('hasSliceData', () => {
  it('is true only when a slice has a positive count', () => {
    expect(hasSliceData(undefined)).toBe(false)
    expect(hasSliceData(null)).toBe(false)
    expect(hasSliceData([{ key: 'a' }, { key: 'b', count: 0 }])).toBe(false)
    expect(hasSliceData([{ key: 'a', count: 1 }])).toBe(true)
  })
})

describe('stackedAreaData', () => {
  it('builds styled filled datasets with formatted bucket labels', () => {
    const data = stackedAreaData(series, 'month', TEST_PALETTE, USER_TYPE_STYLE)

    expect(data.labels).toEqual([
      formatBucketLabel('2026-08-01', 'month'),
      formatBucketLabel('2026-09-01', 'month'),
    ])
    expect(data.datasets).toHaveLength(3)
    expect(data.datasets[0]).toMatchObject({
      label: t('pages.admin.dashboard.series.member'),
      data: [1, 2],
      borderColor: 'orange',
      backgroundColor: 'orange-soft',
      pointBackgroundColor: 'orange',
      fill: true,
      tension: 0.35,
      pointRadius: 0,
    })
  })

  it('falls back to the raw key and the dim color for unstyled series', () => {
    const data = stackedAreaData(series, 'day', TEST_PALETTE, USER_TYPE_STYLE)

    expect(data.labels?.[0]).toBe(formatBucketLabel('2026-08-01', 'day'))
    expect(data.datasets[1]).toMatchObject({
      label: 'unknown',
      borderColor: 'text-dim',
      backgroundColor: 'text-dim',
    })
    expect(data.datasets[2]).toMatchObject({ label: '', data: [] })
  })

  it('returns empty labels and datasets without a series', () => {
    expect(stackedAreaData(undefined, 'month', TEST_PALETTE, USER_TYPE_STYLE)).toEqual({
      labels: [],
      datasets: [],
    })
  })
})

describe('barSeriesData', () => {
  it('builds one bar dataset per series with styled or fallback colors', () => {
    const data = barSeriesData(series, 'month', TEST_PALETTE, USER_TYPE_STYLE)

    expect(data.labels).toHaveLength(2)
    expect(data.datasets[0]).toMatchObject({
      label: t('pages.admin.dashboard.series.member'),
      data: [1, 2],
      backgroundColor: 'orange',
      borderColor: 'surface',
      borderRadius: 3,
      borderSkipped: false,
    })
    expect(data.datasets[1]).toMatchObject({ label: 'unknown', backgroundColor: 'text-dim' })
    expect(data.datasets[2]).toMatchObject({ label: '', data: [] })
  })

  it('returns empty labels and datasets without a series', () => {
    expect(barSeriesData({}, 'month', TEST_PALETTE, CONTENT_STYLE)).toEqual({
      labels: [],
      datasets: [],
    })
  })
})

describe('doughnutData', () => {
  it('drops empty slices and prefers the style map over the API label and color', () => {
    const data = doughnutData(
      [
        { key: 'member', label: 'API member', color: '#111111', count: 4 },
        { key: 'other', label: 'API other', color: '#222222', count: 2 },
        { key: 'plain', count: 1 },
        { label: 'no key', count: 0 },
        { key: 'missing-count' },
      ],
      TEST_PALETTE,
      USER_TYPE_STYLE,
    )

    expect(data.labels).toEqual([t('pages.admin.dashboard.series.member'), 'API other', 'plain'])
    expect(data.datasets[0]).toMatchObject({
      data: [4, 2, 1],
      backgroundColor: ['orange', '#222222', 'text-dim'],
      borderColor: 'surface',
      hoverOffset: 6,
    })
  })

  it('uses the API values without a style map and handles missing keys', () => {
    const data = doughnutData(
      [{ label: 'Robótica', color: '#00ff00', count: 3 }, { count: 2 }],
      TEST_PALETTE,
    )

    expect(data.labels).toEqual(['Robótica', ''])
    expect(data.datasets[0]?.backgroundColor).toEqual(['#00ff00', 'text-dim'])
  })

  it('returns an empty dataset for missing slices', () => {
    expect(doughnutData(null, TEST_PALETTE).labels).toEqual([])
    expect(doughnutData(undefined, TEST_PALETTE).datasets[0]?.data).toEqual([])
  })
})

describe('wrapLabel', () => {
  it('returns a single empty line for blank text', () => {
    expect(wrapLabel('')).toEqual([''])
    expect(wrapLabel('   ')).toEqual([''])
  })

  it('keeps short text on one line', () => {
    expect(wrapLabel('Hackathon de primavera')).toEqual(['Hackathon de primavera'])
  })

  it('wraps words onto a second line without an ellipsis when everything fits', () => {
    expect(wrapLabel('Taller de robótica avanzada para jóvenes')).toEqual([
      'Taller de robótica',
      'avanzada para jóvenes',
    ])
  })

  it('truncates the last line with an ellipsis when words are cut off', () => {
    expect(wrapLabel('one two three four five six', 10, 2)).toEqual(['one two', 'three fou…'])
    expect(wrapLabel('hello world again', 10, 1)).toEqual(['hello…'])
  })

  it('does not add an ellipsis for surrounding whitespace', () => {
    expect(wrapLabel(' Hackathon ')).toEqual(['Hackathon'])
  })

  it('shortens a single word longer than a line', () => {
    expect(wrapLabel('Supercalifragilistico', 10)).toEqual(['Supercali…'])
  })
})

describe('rankingBarData', () => {
  it('builds a single colored dataset of confirmed inscriptions', () => {
    const data = rankingBarData(
      [['Hackathon', 'de primavera'], 'Taller'],
      [42, 7],
      '#f60',
      TEST_PALETTE,
    )

    expect(data.labels).toEqual([['Hackathon', 'de primavera'], 'Taller'])
    expect(data.datasets).toEqual([
      expect.objectContaining({
        label: t('pages.admin.dashboard.series.confirmedDataset'),
        data: [42, 7],
        backgroundColor: '#f60',
        borderColor: 'surface',
        borderRadius: 4,
      }),
    ])
  })
})

describe('areaOptions', () => {
  it('stacks both axes, shows the legend and formats the tooltip value', () => {
    const options = inspect(areaOptions(TEST_PALETTE))

    expect(areaOptions(TEST_PALETTE)).toMatchObject({
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
    })
    expect(options.plugins.legend).toMatchObject({ display: true, labels: { color: 'text-muted' } })
    expect(options.plugins.tooltip.backgroundColor).toBe('surface')
    expect(options.scales.x).toMatchObject({ stacked: true, ticks: { autoSkip: true } })
    expect(options.scales.y).toMatchObject({ stacked: true, beginAtZero: true })

    const label = options.plugins.tooltip.callbacks.label
    expect(label({ dataset: { label: 'Socios' }, parsed: { y: 12345 } })).toBe(' Socios: 12.345')
    expect(label({ dataset: {}, parsed: { y: 3 } })).toBe(' : 3')
  })
})

describe('barOptions', () => {
  it.each([true, false])('sets stacked=%s on both axes', (stacked) => {
    const options = inspect(barOptions(TEST_PALETTE, stacked))

    expect(options.scales.x?.stacked).toBe(stacked)
    expect(options.scales.y?.stacked).toBe(stacked)
    expect(options.plugins.legend.display).toBe(true)
  })

  it('formats the tooltip value with the dataset label', () => {
    const { label } = inspect(barOptions(TEST_PALETTE, false)).plugins.tooltip.callbacks

    expect(label({ dataset: { label: 'Recursos' }, parsed: { y: 4 } })).toBe(' Recursos: 4')
    expect(label({ dataset: {}, parsed: { y: 0 } })).toBe(' : 0')
  })
})

describe('rankingOptions', () => {
  it('draws horizontal bars without legend and without skipping category labels', () => {
    const raw = rankingOptions(TEST_PALETTE, ['Uno', 'Dos'])
    const options = inspect(raw)

    expect(raw.indexAxis).toBe('y')
    expect(options.plugins.legend.display).toBe(false)
    expect(options.scales.y).toMatchObject({ stacked: false, ticks: { autoSkip: false } })
    expect(options.scales.x).toMatchObject({ stacked: false, beginAtZero: true })
  })

  it('shows the unwrapped title and the confirmed count in the tooltip', () => {
    const { title, label } = inspect(rankingOptions(TEST_PALETTE, ['Uno', 'Dos'])).plugins.tooltip
      .callbacks

    expect(title([{ dataIndex: 1 }])).toBe('Dos')
    expect(title([])).toBe('Uno')
    expect(title([{ dataIndex: 5 }])).toBe('')
    expect(label({ parsed: { x: 1500 } })).toBe(
      t('pages.admin.dashboard.tooltip.ranking', { n: '1500' }),
    )
  })
})

describe('doughnutOptions', () => {
  it('uses a cutout and shows the legend', () => {
    const raw = doughnutOptions(TEST_PALETTE)

    expect(raw.cutout).toBe('62%')
    expect(inspect(raw).plugins.legend.display).toBe(true)
  })

  it('computes the percentage over the visible slices only', () => {
    const { label } = inspect(doughnutOptions(TEST_PALETTE)).plugins.tooltip.callbacks
    const hidden = new Set([2])
    const item = {
      label: 'Adultos',
      parsed: 30,
      dataset: { data: [30, 10, 60] },
      chart: { getDataVisibility: (index: number) => !hidden.has(index) },
    }

    expect(label(item)).toBe(
      t('pages.admin.dashboard.tooltip.slice', { label: 'Adultos', n: '30', p: 75 }),
    )
  })

  it('treats missing values as zero and reports 0% when nothing is visible', () => {
    const { label } = inspect(doughnutOptions(TEST_PALETTE)).plugins.tooltip.callbacks

    expect(
      label({
        label: 'Menores',
        parsed: 0,
        dataset: { data: [null, 0] },
        chart: { getDataVisibility: () => true },
      }),
    ).toBe(t('pages.admin.dashboard.tooltip.slice', { label: 'Menores', n: '0', p: 0 }))
  })
})
