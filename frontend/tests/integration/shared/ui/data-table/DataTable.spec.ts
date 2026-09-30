import { defineComponent, h } from 'vue'
import { ElPagination, ElTableColumn } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { useServerTable, type ServerTablePage } from '@/shared/lib/paging'
import { DataTable } from '@/shared/ui/data-table'

import { renderWithProviders } from '../../../../support/render'

interface Row {
  readonly id: string
  readonly name: string
}

function renderTable(
  fetchPage: (params: Record<string, unknown>) => Promise<ServerTablePage<Row>>,
) {
  const Host = defineComponent({
    setup() {
      const table = useServerTable<Row>({ queryKey: ['rows'], fetchPage })
      return () =>
        h(
          DataTable<Row>,
          { table, emptyText: 'Nothing yet', errorText: 'Could not load' },
          { default: () => h(ElTableColumn, { prop: 'name', label: 'Name' }) },
        )
    },
  })
  return renderWithProviders(Host)
}

describe('DataTable', () => {
  it('shows the rows of the current page and pages through the server', async () => {
    const fetchPage = vi.fn((params: Record<string, unknown>) =>
      Promise.resolve({ items: [{ id: `row-${String(params.page)}`, name: 'Ada' }], total: 60 }),
    )
    const { wrapper } = await renderTable(fetchPage)

    expect(wrapper.findAll('.el-table__body tr')).toHaveLength(1)
    expect(wrapper.text()).toContain('Ada')

    wrapper.findComponent(ElPagination).vm.$emit('current-change', 2)
    await vi.waitFor(() =>
      expect(fetchPage).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 })),
    )
  })

  it('tells an empty list from one that failed to load', async () => {
    const empty = await renderTable(() => Promise.resolve({ items: [], total: 0 }))
    expect(empty.wrapper.text()).toContain('Nothing yet')

    const failed = await renderTable(() => Promise.reject(new Error('offline')))
    await vi.waitFor(() => expect(failed.wrapper.text()).toContain('Could not load'))
  })
})
