import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { DiagnosticsPage } from '@/pages/admin/diagnostics'
import type { DiagnosticLogEntryResponse } from '@/shared/api/generated/models'
import { formatDateTimeSeconds } from '@/shared/lib/date'
import { ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'
import { SearchInput } from '@/shared/ui/search-input'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import {
  buildDiagnosticDetail,
  buildDiagnosticEntry,
  TRACE_ID,
} from '../../../../../support/builders'
import {
  click,
  findButton,
  findDialog,
  openRowMenu,
  rowMenuItem,
  textOf,
} from '../../../../../support/dom'

async function renderPage(entries: DiagnosticLogEntryResponse[], urls: URL[] = []) {
  server.use(
    http.get('/api/logs/diagnostics', ({ request }) => {
      urls.push(new URL(request.url))
      return HttpResponse.json(paged(entries))
    }),
  )
  const rendered = await renderWithProviders(DiagnosticsPage, {
    route: '/admin/diagnostics',
    attach: true,
  })
  await flushPromises()
  return rendered
}

const plainEntry = buildDiagnosticEntry({
  id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a60',
  level: 'Warning',
  category: 'CodigoActivo.Application.Common.Messaging.LoggingQueryDecorator',
  eventName: null,
  message: 'GetUserQuery took 5432 ms',
  hasException: false,
  traceId: null,
})

describe('DiagnosticsPage', () => {
  it('lists each entry with its instant, level, source, message and trace', async () => {
    const { wrapper } = await renderPage([buildDiagnosticEntry(), plainEntry])

    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(2))
    const [failure, slow] = wrapper.findAll('.el-table__body tr')
    expect(failure?.text()).toContain(formatDateTimeSeconds('2026-07-04T09:02:05Z'))
    expect(failure?.text()).toContain(t('pages.admin.diagnostics.levels.Error'))
    expect(failure?.find('.table-primary-cell__title').text()).toBe('UnhandledRequestException')
    expect(failure?.text()).toContain('CodigoActivo.API.Middlewares.GlobalExceptionHandler')
    expect(failure?.text()).toContain(t('pages.admin.diagnostics.exception'))
    expect(failure?.find('.trace').text()).toBe(TRACE_ID)
    expect(slow?.find('.table-primary-cell__title').text()).toBe(
      'CodigoActivo.Application.Common.Messaging.LoggingQueryDecorator',
    )
    expect(slow?.text()).not.toContain(t('pages.admin.diagnostics.exception'))
    expect(slow?.find('.trace').exists()).toBe(false)
  })

  it('searches messages, filters by level and source and sorts by the newest first', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderPage([buildDiagnosticEntry()], urls)
    expect(urls[0]?.searchParams.get('sort')).toBe('-occurredAt')

    wrapper.findComponent(SearchInput).vm.$emit('update:modelValue', 'boom')
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('search')).toBe('boom'))

    const level = wrapper.findComponent(ColumnFilterSelect)
    expect(level.props('options')).toHaveLength(6)
    level.vm.$emit('update:modelValue', 'Error')
    level.vm.$emit('apply')
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('level')).toBe('Error'))

    const source = wrapper.findComponent(ColumnSearch)
    source.vm.$emit('update:modelValue', 'Handler')
    source.vm.$emit('apply')
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('category')).toBe('Handler'))
  })

  it('narrows the list to a trace from its cell and lifts it from the chip', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderPage([buildDiagnosticEntry()], urls)
    await vi.waitFor(() => expect(wrapper.find('.trace').exists()).toBe(true))

    await wrapper.find('.trace').trigger('click')

    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('traceId')).toBe(TRACE_ID))
    expect(wrapper.text()).toContain(
      t('pages.admin.diagnostics.filters.trace', { trace: TRACE_ID }),
    )
  })

  it('shows the exception and template values of an entry as text', async () => {
    server.use(
      http.get('/api/logs/diagnostics/:id', () =>
        HttpResponse.json(buildDiagnosticDetail({ exception: '<script>alert(1)</script> boom' })),
      ),
    )
    const { wrapper } = await renderPage([buildDiagnosticEntry()])
    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(1))

    await click(findButton(t('pages.admin.diagnostics.viewDetail')))

    const title = t('pages.admin.diagnostics.detail.title')
    await vi.waitFor(() => expect(findDialog(title)?.querySelectorAll('pre')).toHaveLength(2))
    const blocks = [...(findDialog(title)?.querySelectorAll('pre') ?? [])].map(textOf)
    expect(blocks[0]).toContain('"RouteTemplate": "api/users/{id}"')
    expect(blocks[1]).toBe('<script>alert(1)</script> boom')
    expect(findDialog(title)?.querySelector('script')).toBeNull()
  })

  it('says when an entry has no exception and when it can no longer be loaded', async () => {
    server.use(
      http.get('/api/logs/diagnostics/:id', ({ params }) =>
        params.id === plainEntry.id
          ? HttpResponse.json(
              buildDiagnosticDetail({
                id: plainEntry.id,
                exception: null,
                properties: null,
                eventName: null,
                traceId: null,
              }),
            )
          : apiError(404, 'DiagnosticLogEntryNotFound'),
      ),
    )
    const { wrapper } = await renderPage([plainEntry, buildDiagnosticEntry()])
    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(2))
    const title = t('pages.admin.diagnostics.detail.title')

    const [plain, missing] = wrapper.findAll('.el-table__body tr')
    await click(findButton(t('pages.admin.diagnostics.viewDetail'), plain?.element))
    await vi.waitFor(() =>
      expect(textOf(findDialog(title) ?? null)).toContain(
        t('pages.admin.diagnostics.detail.noException'),
      ),
    )

    await click(findButton(t('pages.admin.diagnostics.viewDetail'), missing?.element))
    await vi.waitFor(() =>
      expect(textOf(findDialog(title) ?? null)).toContain(
        t('pages.admin.diagnostics.detail.error'),
      ),
    )
  })

  it('opens the audit of a trace and refuses it for an entry without one', async () => {
    const { wrapper, router } = await renderPage([plainEntry, buildDiagnosticEntry()])
    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(2))
    const [plain, traced] = wrapper.findAll('.el-table__body tr')

    const plainMenu = await openRowMenu(plain!.element)
    expect(rowMenuItem(plainMenu, t('pages.admin.diagnostics.openAudit')).className).toContain(
      'is-disabled',
    )
    await click(document.body)

    const tracedMenu = await openRowMenu(traced!.element)
    await click(rowMenuItem(tracedMenu, t('pages.admin.diagnostics.openAudit')))

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('admin-audit'))
    expect(router.currentRoute.value.query).toEqual({ traceId: TRACE_ID })
  })
})
