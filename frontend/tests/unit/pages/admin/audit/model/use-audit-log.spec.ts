import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { toAuditEntry } from '@/pages/admin/audit/api/mapper'
import { useAuditLog } from '@/pages/admin/audit/model/use-audit-log'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildAuditEntry, TRACE_ID } from '../../../../../support/builders'
import { mountComposable, t } from '../../../../../support/render'

function recordRequests(): URL[] {
  const urls: URL[] = []
  server.use(
    http.get('/api/logs/audit', ({ request }) => {
      urls.push(new URL(request.url))
      return HttpResponse.json(paged([buildAuditEntry()]))
    }),
  )
  return urls
}

describe('useAuditLog', () => {
  it('lists the newest entries first and takes the trace from the address', async () => {
    const urls = recordRequests()

    const { result } = await mountComposable(useAuditLog, {
      route: `/admin/audit?traceId=${TRACE_ID}`,
    })

    await vi.waitFor(() => expect(urls).toHaveLength(1))
    expect(urls[0]?.searchParams.get('sort')).toBe('-occurredAt')
    expect(urls[0]?.searchParams.get('traceId')).toBe(TRACE_ID)
    expect(result.extraFilters.value.map((chip) => chip.label)).toEqual([
      t('pages.admin.audit.filters.trace', { trace: TRACE_ID }),
    ])
  })

  it('pins an actor, an entity, a related entity and an address as removable filters', async () => {
    const urls = recordRequests()
    const { result } = await mountComposable(useAuditLog, { route: '/admin/audit' })
    const entry = toAuditEntry(buildAuditEntry())

    result.filterByActor(entry)
    result.filterByEntity(entry.entity!)
    result.filterByIp('203.0.113.9')
    await flushPromises()

    await vi.waitFor(() =>
      expect(Object.fromEntries(urls.at(-1)?.searchParams ?? [])).toMatchObject({
        actorId: 'admin-1',
        entityId: 'user-2',
        clientIp: '203.0.113.9',
      }),
    )
    expect(result.extraFilters.value.map((chip) => chip.label)).toEqual([
      t('pages.admin.audit.filters.actor', { actor: 'Ada Admin' }),
      t('pages.admin.audit.filters.entity', { entity: 'Marta Miembro' }),
      t('pages.admin.audit.filters.ip', { ip: '203.0.113.9' }),
    ])

    result.filterByEntity(entry.related[0]!)
    expect(result.extraFilters.value[1]?.label).toBe(
      t('pages.admin.audit.filters.entity', { entity: 'Taller de robótica' }),
    )

    for (const chip of result.extraFilters.value) chip.remove()
    await flushPromises()

    expect(result.extraFilters.value).toEqual([])
    await vi.waitFor(() =>
      expect([...(urls.at(-1)?.searchParams.keys() ?? [])]).toEqual(['page', 'pageSize', 'sort']),
    )
  })

  it('does not pin an actor without an account', async () => {
    recordRequests()
    const { result } = await mountComposable(useAuditLog, { route: '/admin/audit' })

    result.filterByActor(toAuditEntry(buildAuditEntry({ actorType: 'System', actorId: null })))

    expect(result.extraFilters.value).toEqual([])
  })

  it('keeps a pinned trace in the address and clears it from its chip', async () => {
    recordRequests()
    const { result, router } = await mountComposable(useAuditLog, { route: '/admin/audit' })

    result.filterByTrace(TRACE_ID)
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ traceId: TRACE_ID })

    result.extraFilters.value[0]?.remove()
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({})

    await router.replace({ query: { traceId: TRACE_ID } })
    await flushPromises()
    expect(result.extraFilters.value).toHaveLength(1)
  })

  it('opens the diagnostic page on the same trace', async () => {
    recordRequests()
    const { result, router } = await mountComposable(useAuditLog, { route: '/admin/audit' })

    result.openDiagnostics(TRACE_ID)
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('admin-diagnostics')
    expect(router.currentRoute.value.query).toEqual({ traceId: TRACE_ID })
  })
})
