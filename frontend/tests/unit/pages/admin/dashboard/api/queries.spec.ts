import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { useQuery } from '@tanstack/vue-query'
import { describe, expect, it, vi } from 'vitest'

import { dashboardKeys, dashboardQueries } from '@/pages/admin/dashboard/api/queries'

import { http, HttpResponse, server } from '../../../../../support/server'
import { buildDashboardAnalytics } from '../../../../../support/builders'
import { createTestQueryClient, mountComposable } from '../../../../../support/render'

describe('dashboardKeys', () => {
  it('keys the analytics by range under the reports root', () => {
    expect(dashboardKeys.analytics({ from: '2026-01-01', to: '2026-01-31' })).toEqual([
      'reports',
      'dashboard-analytics',
      '2026-01-01',
      '2026-01-31',
    ])
  })
})

describe('dashboardQueries', () => {
  it('requests the analytics of the range', async () => {
    const requests: string[] = []
    server.use(
      http.get('/api/reports/dashboard/analytics', ({ request }) => {
        requests.push(new URL(request.url).search)
        return HttpResponse.json(buildDashboardAnalytics({ granularity: 'day' }))
      }),
    )

    const analytics = await createTestQueryClient().fetchQuery(
      dashboardQueries.analytics({ from: '2026-08-19', to: '2026-09-17' }),
    )

    expect(analytics.granularity).toBe('day')
    expect(requests).toEqual(['?from=2026-08-19&to=2026-09-17'])
  })

  it('keeps the previous range shown while a new one loads', async () => {
    let release: (() => void) | undefined
    server.use(
      http.get('/api/reports/dashboard/analytics', async ({ request }) => {
        const from = new URL(request.url).searchParams.get('from') ?? ''
        if (from === '2026-01-01') {
          await new Promise<void>((resolve) => {
            release = resolve
          })
        }
        return HttpResponse.json(buildDashboardAnalytics({ granularity: from }))
      }),
    )
    const range = ref({ from: '2025-09-17', to: '2026-09-17' })

    const { result } = await mountComposable(() =>
      useQuery(() => dashboardQueries.analytics(range.value)),
    )
    await vi.waitFor(() => expect(result.data.value?.granularity).toBe('2025-09-17'))

    range.value = { from: '2026-01-01', to: '2026-02-01' }
    await vi.waitFor(() => expect(release).toBeDefined())
    expect(result.isPlaceholderData.value).toBe(true)
    expect(result.data.value?.granularity).toBe('2025-09-17')

    release?.()
    await vi.waitFor(() => expect(result.data.value?.granularity).toBe('2026-01-01'))
    await flushPromises()
    expect(result.isPlaceholderData.value).toBe(false)
  })
})
