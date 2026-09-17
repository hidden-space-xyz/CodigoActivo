import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { RANGE_OPTIONS, useDashboardRange } from '@/pages/admin/dashboard/model/useDashboardRange'

import { t } from '../../../../support/render'

describe('useDashboardRange', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 8, 17, 15, 30))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('lists the three translated presets', () => {
    expect(RANGE_OPTIONS).toEqual([
      { value: '30d', label: t('pages.admin.dashboard.range.preset30d') },
      { value: '90d', label: t('pages.admin.dashboard.range.preset90d') },
      { value: '12m', label: t('pages.admin.dashboard.range.preset12m') },
    ])
  })

  it('defaults to the last 12 months ending today', () => {
    const { preset, customRange, range } = useDashboardRange()

    expect(preset.value).toBe('12m')
    expect(customRange.value).toBeNull()
    expect(range.value).toEqual({ from: '2025-09-17', to: '2026-09-17' })
  })

  it('spans 30 and 90 days inclusive of today', () => {
    const { range, setPreset } = useDashboardRange()

    setPreset('30d')
    expect(range.value).toEqual({ from: '2026-08-19', to: '2026-09-17' })

    setPreset('90d')
    expect(range.value).toEqual({ from: '2026-06-20', to: '2026-09-17' })
  })

  it('uses the picked dates for a custom range and switches the preset to custom', () => {
    const { preset, range, setCustomRange } = useDashboardRange()

    setCustomRange([new Date(2026, 0, 5), new Date(2026, 1, 10)])

    expect(preset.value).toBe('custom')
    expect(range.value).toEqual({ from: '2026-01-05', to: '2026-02-10' })
  })

  it('ends a custom range without end date today', () => {
    const { range, setCustomRange } = useDashboardRange()

    setCustomRange([new Date(2026, 6, 1), null])

    expect(range.value).toEqual({ from: '2026-07-01', to: '2026-09-17' })
  })

  it('falls back to 12 months when the custom dates are cleared or incomplete', () => {
    const { preset, range, setCustomRange } = useDashboardRange()

    setCustomRange([new Date(2026, 6, 1), new Date(2026, 6, 2)])
    setCustomRange(null)
    expect(preset.value).toBe('12m')

    setCustomRange([null, new Date(2026, 6, 2)])
    expect(preset.value).toBe('12m')
    expect(range.value).toEqual({ from: '2025-09-17', to: '2026-09-17' })
  })

  it('clears the custom dates when a fixed preset is chosen but keeps them for custom', () => {
    const { customRange, range, setCustomRange, setPreset } = useDashboardRange()
    const dates = [new Date(2026, 3, 1), new Date(2026, 3, 30)]

    setCustomRange(dates)
    setPreset('custom')
    expect(customRange.value).toEqual(dates)

    setPreset('30d')
    expect(customRange.value).toBeNull()

    setPreset('custom')
    expect(range.value).toEqual({ from: '2025-09-17', to: '2026-09-17' })
  })
})
