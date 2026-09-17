import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useDashboardAnalytics } from '@/pages/admin/dashboard/model/useDashboardAnalytics'
import type { DashboardAnalyticsResponse } from '@/shared/api/generated/models'

import { buildDashboardAnalytics } from '../../../../support/fixtures/public-dashboard/builders'
import { mountComposable } from '../../../../support/fixtures/public-dashboard/composable'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

describe('useDashboardAnalytics', () => {
  it('requests the analytics for the given range', async () => {
    const requests: string[] = []
    server.use(
      http.get('/api/reports/dashboard/analytics', ({ request }) => {
        requests.push(new URL(request.url).search)
        return HttpResponse.json(buildDashboardAnalytics({ granularity: 'day' }))
      }),
    )

    const { result } = await mountComposable(() =>
      useDashboardAnalytics({ from: '2026-08-19', to: '2026-09-17' }),
    )

    await vi.waitFor(() => expect(result.data.value?.granularity).toBe('day'))
    expect(requests).toEqual(['?from=2026-08-19&to=2026-09-17'])
  })

  it('refetches when the range changes and keeps the previous data meanwhile', async () => {
    let release: (() => void) | undefined
    server.use(
      http.get('/api/reports/dashboard/analytics', async ({ request }) => {
        const from = new URL(request.url).searchParams.get('from') ?? ''
        if (from === '2026-01-01') {
          await new Promise<void>((resolve) => {
            release = resolve
          })
        }
        const body: DashboardAnalyticsResponse = buildDashboardAnalytics({ rangeStart: from })
        return HttpResponse.json(body)
      }),
    )
    const range = ref({ from: '2025-09-17', to: '2026-09-17' })

    const { result } = await mountComposable(() => useDashboardAnalytics(range))
    await vi.waitFor(() => expect(result.data.value?.rangeStart).toBe('2025-09-17'))

    range.value = { from: '2026-01-01', to: '2026-02-01' }
    await vi.waitFor(() => expect(release).toBeDefined())
    expect(result.isFetching.value).toBe(true)
    expect(result.isPlaceholderData.value).toBe(true)
    expect(result.data.value?.rangeStart).toBe('2025-09-17')

    release?.()
    await vi.waitFor(() => expect(result.data.value?.rangeStart).toBe('2026-01-01'))
    await flushPromises()
    expect(result.isPlaceholderData.value).toBe(false)
  })

  it('exposes the error state when the request fails', async () => {
    server.use(http.get('/api/reports/dashboard/analytics', () => apiError(500)))

    const { result } = await mountComposable(() =>
      useDashboardAnalytics(() => ({ from: '2026-01-01', to: '2026-01-31' })),
    )

    await vi.waitFor(() => expect(result.isError.value).toBe(true))
  })
})
