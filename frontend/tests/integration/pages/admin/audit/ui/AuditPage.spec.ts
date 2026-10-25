import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { AuditPage } from '@/pages/admin/audit'
import type { AuditLogEntryResponse } from '@/shared/api/generated/models'
import { formatDateTimeSeconds } from '@/shared/lib/date'
import { ColumnFilterSelect } from '@/shared/ui/column-filter'
import { SearchInput } from '@/shared/ui/search-input'

import { renderWithProviders, t } from '../../../../../support/render'
import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildAuditEntry, omit, TRACE_ID } from '../../../../../support/builders'
import { click, findButton } from '../../../../../support/dom'

async function renderPage(entries: AuditLogEntryResponse[], urls: URL[] = []) {
  server.use(
    http.get('/api/logs/audit', ({ request }) => {
      urls.push(new URL(request.url))
      return HttpResponse.json(paged(entries))
    }),
  )
  const rendered = await renderWithProviders(AuditPage, { route: '/admin/audit', attach: true })
  await flushPromises()
  return rendered
}

const purge = omit(
  buildAuditEntry({
    id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a70',
    actorType: 'System',
    actorId: null,
    actorName: null,
    actorEmail: null,
    clientIp: null,
    action: 'PurgeDataHistory',
    entityId: null,
    entityName: null,
    related: [],
    traceId: null,
  }),
  'entityType',
)

const refusedLogin = buildAuditEntry({
  id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a71',
  actorType: 'Anonymous',
  actorId: null,
  actorName: null,
  actorEmail: null,
  action: 'Login',
  entityName: null,
  related: [],
  outcome: 'Rejected',
  errorCode: 'InvalidCredentials',
})

function filterSelect(wrapper: Awaited<ReturnType<typeof renderPage>>['wrapper'], label: string) {
  const select = wrapper
    .findAllComponents(ColumnFilterSelect)
    .find((candidate) => candidate.props('label') === label)
  if (!select) throw new Error(`No filter labelled ${label}`)
  return select
}

describe('AuditPage', () => {
  it('lists who did what, from where, on what and how it ended', async () => {
    const { wrapper } = await renderPage([buildAuditEntry(), purge, refusedLogin])

    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(3))
    const [setAdmin, system, login] = wrapper.findAll('.el-table__body tr')
    expect(setAdmin?.text()).toContain(formatDateTimeSeconds('2026-07-04T09:01:00Z'))
    expect(setAdmin?.text()).toContain('Ada Admin')
    expect(setAdmin?.text()).toContain('admin@example.com')
    expect(setAdmin?.text()).toContain('203.0.113.9')
    expect(setAdmin?.text()).toContain(t('pages.admin.audit.actions.SetAdmin'))
    expect(setAdmin?.text()).toContain('Marta Miembro')
    expect(setAdmin?.text()).toContain(
      `${t('pages.admin.audit.entityTypes.Activity')}: Taller de robótica`,
    )
    expect(setAdmin?.text()).not.toContain('aaaaaaaa')
    expect(setAdmin?.text()).toContain(t('pages.admin.audit.outcomes.Succeeded'))
    expect(setAdmin?.find('.trace').text()).toBe(TRACE_ID.slice(0, 8))
    expect(setAdmin?.find('.trace').attributes('title')).toBe(TRACE_ID)
    expect(system?.text()).toContain(t('pages.admin.audit.actorTypes.System'))
    expect(system?.findAll('button.pin')).toHaveLength(0)
    expect(login?.text()).toContain(t('pages.admin.audit.actorTypes.Anonymous'))
    expect(login?.text()).toContain(t('pages.admin.audit.outcomes.Rejected'))
    expect(login?.text()).toContain(t('errors.InvalidCredentials'))
    expect(login?.text()).toContain(t('pages.admin.audit.deletedEntities.User'))
    expect(login?.text()).not.toContain('user-2')
  })

  it('names an erased account and searches people from the toolbar', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderPage([buildAuditEntry({ actorName: null })], urls)
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.audit.deletedEntities.User')),
    )
    expect(urls[0]?.searchParams.get('sort')).toBe('-occurredAt')

    wrapper.findComponent(SearchInput).vm.$emit('update:modelValue', 'marta')

    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('search')).toBe('marta'))
  })

  it('filters by actor kind, command, entity kind and outcome', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderPage([buildAuditEntry()], urls)

    const action = filterSelect(wrapper, t('pages.admin.audit.columns.action'))
    expect(action.props('filterable')).toBe(true)
    const labels = (action.props('options') as { label: string }[]).map((option) => option.label)
    expect(labels).toEqual([...labels].sort((a, b) => a.localeCompare(b, 'es')))

    for (const [label, value, param] of [
      [t('pages.admin.audit.columns.actor'), 'System', 'actorType'],
      [t('pages.admin.audit.columns.action'), 'Login', 'action'],
      [t('pages.admin.audit.columns.entity'), 'Event', 'entityType'],
      [t('pages.admin.audit.columns.outcome'), 'Failed', 'outcome'],
    ] as const) {
      const select = filterSelect(wrapper, label)
      select.vm.$emit('update:modelValue', value)
      select.vm.$emit('apply')
      await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get(param)).toBe(value))
    }
  })

  it('pins the actor, the address, the entity, a related entity and the trace a click names', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderPage([buildAuditEntry()], urls)
    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(1))

    await click(findButton(t('pages.admin.audit.filterActor', { actor: 'Ada Admin' })))
    await click(findButton(t('pages.admin.audit.filterIp', { ip: '203.0.113.9' })))
    await click(findButton(t('pages.admin.audit.filterTrace', { trace: TRACE_ID })))
    await vi.waitFor(() =>
      expect(Object.fromEntries(urls.at(-1)?.searchParams ?? [])).toMatchObject({
        actorId: 'admin-1',
        clientIp: '203.0.113.9',
        traceId: TRACE_ID,
      }),
    )

    await click(findButton(t('pages.admin.audit.filterEntity', { entity: 'Marta Miembro' })))
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('entityId')).toBe('user-2'))

    const related = wrapper.findAll('.related button')
    expect(related).toHaveLength(1)
    await related[0]?.trigger('click')
    await vi.waitFor(() =>
      expect(urls.at(-1)?.searchParams.get('entityId')).toBe(
        'aaaaaaaa-1111-1111-1111-111111111111',
      ),
    )
    expect(wrapper.text()).toContain(
      t('pages.admin.audit.filters.entity', { entity: 'Taller de robótica' }),
    )
  })

  it('opens the diagnostics of a trace from the row', async () => {
    const { wrapper, router } = await renderPage([buildAuditEntry(), purge])
    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(2))
    const [traced, untraced] = wrapper.findAll('.el-table__body tr')

    expect(findButton(t('pages.admin.audit.openDiagnostics'), untraced?.element).disabled).toBe(
      true,
    )
    await click(findButton(t('pages.admin.audit.openDiagnostics'), traced?.element))

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('admin-diagnostics'))
    expect(router.currentRoute.value.query).toEqual({ traceId: TRACE_ID })
  })
})
