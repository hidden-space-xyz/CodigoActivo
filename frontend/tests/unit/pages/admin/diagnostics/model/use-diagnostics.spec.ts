import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useDiagnostics } from '@/pages/admin/diagnostics/model/use-diagnostics'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import {
  buildDiagnosticDetail,
  buildDiagnosticEntry,
  TRACE_ID,
} from '../../../../../support/builders'
import { mountComposable, t } from '../../../../../support/render'
import { toDiagnosticEntry } from '@/pages/admin/diagnostics/api/mapper'

function recordRequests(): URL[] {
  const urls: URL[] = []
  server.use(
    http.get('/api/logs/diagnostics', ({ request }) => {
      urls.push(new URL(request.url))
      return HttpResponse.json(paged([buildDiagnosticEntry()]))
    }),
    http.get('/api/logs/diagnostics/:id', () => HttpResponse.json(buildDiagnosticDetail())),
  )
  return urls
}

describe('useDiagnostics', () => {
  it('lists the newest entries first without a trace filter', async () => {
    const urls = recordRequests()

    const { result } = await mountComposable(useDiagnostics, { route: '/admin/diagnostics' })

    await vi.waitFor(() => expect(urls).toHaveLength(1))
    expect(urls[0]?.searchParams.get('sort')).toBe('-occurredAt')
    expect(urls[0]?.searchParams.has('traceId')).toBe(false)
    expect(result.extraFilters.value).toEqual([])
  })

  it('takes the trace from the address and shows it as a removable filter', async () => {
    const urls = recordRequests()

    const { result, router } = await mountComposable(useDiagnostics, {
      route: `/admin/diagnostics?traceId=${TRACE_ID.toUpperCase()}`,
    })

    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('traceId')).toBe(TRACE_ID))
    const [chip] = result.extraFilters.value
    expect(chip?.label).toBe(t('pages.admin.diagnostics.filters.trace', { trace: TRACE_ID }))

    chip?.remove()
    await flushPromises()

    expect(result.extraFilters.value).toEqual([])
    expect(router.currentRoute.value.query).toEqual({})
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.has('traceId')).toBe(false))
  })

  it('ignores an address trace that is not one and follows later address changes', async () => {
    const urls = recordRequests()

    const { result, router } = await mountComposable(useDiagnostics, {
      route: '/admin/diagnostics?traceId=not-a-trace',
    })
    await vi.waitFor(() => expect(urls).toHaveLength(1))
    expect(urls[0]?.searchParams.has('traceId')).toBe(false)
    expect(result.extraFilters.value).toEqual([])

    await router.replace({ query: { traceId: TRACE_ID } })
    await flushPromises()

    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('traceId')).toBe(TRACE_ID))
    expect(result.extraFilters.value).toHaveLength(1)
  })

  it('filters by a trace and keeps it in the address', async () => {
    recordRequests()
    const { result, router } = await mountComposable(useDiagnostics, {
      route: '/admin/diagnostics',
    })

    result.filterByTrace(TRACE_ID)
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ traceId: TRACE_ID })
  })

  it('loads the entry of the dialog only once it is opened', async () => {
    const requested: string[] = []
    recordRequests()
    server.use(
      http.get('/api/logs/diagnostics/:id', ({ params }) => {
        requested.push(String(params.id))
        return HttpResponse.json(buildDiagnosticDetail())
      }),
    )
    const { result } = await mountComposable(useDiagnostics, { route: '/admin/diagnostics' })
    expect(requested).toEqual([])

    result.openDetail(toDiagnosticEntry(buildDiagnosticEntry()))

    expect(result.detail.visible.value).toBe(true)
    await vi.waitFor(() => expect(result.detail.entry.value?.exception).toContain('boom'))
    expect(requested).toEqual(['0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b'])
  })

  it('opens the audit page on the same trace', async () => {
    recordRequests()
    const { result, router } = await mountComposable(useDiagnostics, {
      route: '/admin/diagnostics',
    })

    result.openAudit(TRACE_ID)
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('admin-audit')
    expect(router.currentRoute.value.query).toEqual({ traceId: TRACE_ID })
  })
})
