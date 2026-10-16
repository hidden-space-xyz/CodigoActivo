import { defineComponent, h, ref, type VNode } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { ElPagination, ElTableColumn } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { useServerTable, type ServerTable, type ServerTablePage } from '@/shared/lib/paging'
import { ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable, type TableFilterChip } from '@/shared/ui/data-table'

import { click, findButton, findButtons, textOf, tp } from '../../../../support/dom'
import { renderWithProviders, t } from '../../../../support/render'

interface Row {
  readonly id: string
  readonly name: string
}

type FetchPage = (params: Record<string, unknown>) => Promise<ServerTablePage<Row>>

interface TableOptions {
  readonly props?: () => Record<string, unknown>
  readonly slots?: (table: ServerTable<Row>) => Record<string, () => VNode | VNode[]>
}

function emailSearch(table: ServerTable<Row>): VNode {
  return h(ColumnSearch, {
    modelValue: table.filterValue('email') as string | null,
    label: 'Email',
    'onUpdate:modelValue': (value: string | number | null) => {
      table.columnFilter('email').value = value
    },
    onApply: table.onFilter,
  })
}

async function renderTable(fetchPage: FetchPage, options: TableOptions = {}) {
  const box: { table?: ServerTable<Row> } = {}
  const Host = defineComponent({
    setup() {
      const table = useServerTable<Row>({
        queryKey: ['rows'],
        fetchPage,
        columns: { name: {}, email: {} },
      })
      box.table = table
      return () =>
        h(
          DataTable<Row>,
          {
            table,
            emptyText: 'Nothing yet',
            errorText: 'Could not load',
            ...options.props?.(),
          },
          {
            ...options.slots?.(table),
            default: () => [
              h(ElTableColumn, { prop: 'name', label: 'Name', sortable: 'custom' }),
              h(
                ElTableColumn,
                { prop: 'email', label: 'Email' },
                { header: () => emailSearch(table) },
              ),
            ],
          },
        )
    },
  })
  const rendered = await renderWithProviders(Host, { attach: true })
  if (!box.table) throw new Error('Table did not mount')
  return { ...rendered, table: box.table }
}

function deferredPages() {
  const pending: ((page: ServerTablePage<Row>) => void)[] = []
  const fetchPage = vi.fn<FetchPage>(
    () => new Promise<ServerTablePage<Row>>((resolve) => pending.push(resolve)),
  )
  return {
    fetchPage,
    resolve: async (page: ServerTablePage<Row>) => {
      pending.shift()?.(page)
      await flushPromises()
    },
  }
}

function page(total: number): ServerTablePage<Row> {
  return { items: total > 0 ? [{ id: 'row-1', name: 'Ada' }] : [], total }
}

function chipLabels(root: Element): string[] {
  return [...root.querySelectorAll('.filter-chip__label')].map((chip) => textOf(chip))
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

  it('counts the matching rows in the toolbar', async () => {
    const one = await renderTable(() => Promise.resolve(page(1)))
    const many = await renderTable(() => Promise.resolve(page(12345)))

    expect(one.wrapper.find('.data-table__count').text()).toBe(tp('table.results', 1))
    expect(many.wrapper.find('.data-table__count').text()).toBe(
      tp('table.results', 12345, { n: '12.345' }),
    )
    expect(one.wrapper.find('.data-table__count').attributes('aria-live')).toBe('polite')
  })

  it('searches the column bound to the toolbar box from the first page', async () => {
    const fetchPage = vi.fn<FetchPage>(() => Promise.resolve(page(60)))
    const { wrapper, table } = await renderTable(fetchPage, {
      props: () => ({ search: { key: 'name', label: 'Search by name' } }),
    })
    table.onCurrentPageChange(2)
    await flushPromises()

    const input = wrapper.find<HTMLInputElement>('.data-table__search input')
    expect(input.attributes('aria-label')).toBe('Search by name')
    expect(input.attributes('placeholder')).toBe('Search by name')
    await input.setValue('  ada ')
    await input.trigger('keydown', { key: 'Enter' })

    await vi.waitFor(() =>
      expect(fetchPage).toHaveBeenLastCalledWith({ page: 1, pageSize: 25, name: 'ada' }),
    )
    expect(wrapper.find('.filter-chip').exists()).toBe(false)
    expect(findButtons(t('table.clearFilters'), wrapper.element)).toHaveLength(1)

    await input.setValue('')
    await input.trigger('keydown', { key: 'Enter' })

    await vi.waitFor(() => expect(fetchPage).toHaveBeenLastCalledWith({ page: 1, pageSize: 25 }))
    expect(table.filterValue('name')).toBeNull()

    await input.setValue('grace')
    await input.trigger('keydown', { key: 'Enter' })
    table.clearFilters()
    await flushPromises()

    expect(input.element.value).toBe('')
    expect(wrapper.find('.data-table__chips').exists()).toBe(false)
  })

  it('shows a chip for an active header filter whose removal clears it and refetches', async () => {
    const fetchPage = vi.fn<FetchPage>(() => Promise.resolve(page(3)))
    const { wrapper, table } = await renderTable(fetchPage)
    expect(wrapper.find('.data-table__chips').exists()).toBe(false)

    table.columnFilter('email').value = 'ada@'
    table.onFilter()
    await vi.waitFor(() =>
      expect(fetchPage).toHaveBeenLastCalledWith({ page: 1, pageSize: 25, email: 'ada@' }),
    )

    const label = t('table.filterChip', { label: 'Email', value: 'ada@' })
    expect(wrapper.find('.data-table__chips ul').attributes('aria-label')).toBe(
      t('table.activeFilters'),
    )
    expect(chipLabels(wrapper.element)).toEqual([label])

    await click(findButton(t('table.removeFilter', { label }), wrapper.element))

    await vi.waitFor(() => expect(fetchPage).toHaveBeenLastCalledWith({ page: 1, pageSize: 25 }))
    expect(wrapper.find('.data-table__chips').exists()).toBe(false)
  })

  it('lists page-owned filters as chips removed one by one or all at once', async () => {
    const fetchPage = vi.fn<FetchPage>(() => Promise.resolve(page(3)))
    const removeTutor = vi.fn()
    const removeScope = vi.fn()
    const extraFilters: TableFilterChip[] = [
      { key: 'tutor', label: 'Tutor de Ada', remove: removeTutor },
      { key: 'scope', label: 'Próximos', remove: removeScope },
    ]
    const { wrapper, table } = await renderTable(fetchPage, {
      props: () => ({ extraFilters }),
    })

    expect(chipLabels(wrapper.element)).toEqual(['Tutor de Ada', 'Próximos'])

    await click(findButton(t('table.removeFilter', { label: 'Tutor de Ada' }), wrapper.element))
    expect(removeTutor).toHaveBeenCalledTimes(1)
    expect(removeScope).not.toHaveBeenCalled()

    table.columnFilter('email').value = 'ada@'
    table.columnFilter('name').value = 'ada'
    table.onFilter()
    await flushPromises()
    expect(chipLabels(wrapper.element)).toEqual([
      t('table.filterChip', { label: 'Email', value: 'ada@' }),
      'Tutor de Ada',
      'Próximos',
    ])

    await click(findButton(t('table.clearFilters'), wrapper.find('.data-table__chips').element))

    expect(removeTutor).toHaveBeenCalledTimes(2)
    expect(removeScope).toHaveBeenCalledTimes(1)
    expect(table.filterValue('email')).toBeNull()
    expect(table.filterValue('name')).toBeNull()
    await vi.waitFor(() => expect(fetchPage).toHaveBeenLastCalledWith({ page: 1, pageSize: 25 }))
  })

  it('offers to clear the filters when they match nothing', async () => {
    const fetchPage = vi.fn<FetchPage>((params) => Promise.resolve(page(params.email ? 0 : 3)))
    const { wrapper, table } = await renderTable(fetchPage)

    table.columnFilter('email').value = 'nobody'
    table.onFilter()
    await vi.waitFor(() => expect(wrapper.find('.data-table__empty').exists()).toBe(true))

    const empty = wrapper.find('.data-table__empty')
    expect(empty.text()).toContain(t('table.noMatches'))
    expect(empty.attributes('role')).toBeUndefined()

    await click(findButton(t('table.clearFilters'), empty.element))

    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(1))
    expect(table.filterValue('email')).toBeNull()
  })

  it('treats page-owned filters as filtering when nothing matches', async () => {
    const { wrapper } = await renderTable(() => Promise.resolve(page(0)), {
      props: () => ({
        extraFilters: [{ key: 'tier', label: 'Tier 1', remove: () => undefined }],
      }),
    })

    expect(wrapper.find('.data-table__empty').text()).toContain(t('table.noMatches'))
    expect(wrapper.text()).not.toContain('Nothing yet')
  })

  it('keeps keyboard focus in the table when filter chips are removed', async () => {
    const extraFilters = ref<TableFilterChip[]>([])
    extraFilters.value = [
      {
        key: 'tutor',
        label: 'Tutor de Ada',
        remove: () => {
          extraFilters.value = []
        },
      },
    ]
    const { wrapper, table } = await renderTable(() => Promise.resolve(page(3)), {
      props: () => ({ extraFilters: extraFilters.value }),
    })
    table.columnFilter('email').value = 'ada@'
    table.onFilter()
    await flushPromises()
    const emailChip = t('table.filterChip', { label: 'Email', value: 'ada@' })
    expect(chipLabels(wrapper.element)).toEqual([emailChip, 'Tutor de Ada'])

    await click(findButton(t('table.removeFilter', { label: emailChip }), wrapper.element))
    await flushPromises()

    const tutorRemove = findButton(
      t('table.removeFilter', { label: 'Tutor de Ada' }),
      wrapper.element,
    )
    expect(document.activeElement).toBe(tutorRemove)

    await click(tutorRemove)
    await flushPromises()

    expect(wrapper.find('.data-table__chips').exists()).toBe(false)
    expect(document.activeElement).toBe(wrapper.find('.data-table').element)
  })

  it('moves focus to the search box after clearing every filter', async () => {
    const { wrapper, table } = await renderTable(() => Promise.resolve(page(3)), {
      props: () => ({ search: { key: 'name', label: 'Search by name' } }),
    })
    table.columnFilter('email').value = 'ada@'
    table.onFilter()
    await flushPromises()

    await click(findButton(t('table.clearFilters'), wrapper.find('.data-table__chips').element))
    await flushPromises()

    expect(wrapper.find('.data-table__chips').exists()).toBe(false)
    expect(document.activeElement).toBe(wrapper.find('.data-table__search input').element)
  })

  it('retries a failed load without claiming zero results', async () => {
    const fetchPage = vi.fn<FetchPage>(() => Promise.reject(new Error('offline')))
    const { wrapper } = await renderTable(fetchPage)
    await vi.waitFor(() =>
      expect(wrapper.find('.data-table__empty[role="alert"]').text()).toContain('Could not load'),
    )
    expect(wrapper.find('.data-table__count').text()).toBe('')

    await click(findButton(t('common.retry'), wrapper.element))

    await vi.waitFor(() => expect(fetchPage).toHaveBeenCalledTimes(2))
  })

  it('shows a skeleton on the first load and dims the rows while refetching', async () => {
    const { fetchPage, resolve } = deferredPages()
    const { wrapper, table } = await renderTable(fetchPage)
    const root = wrapper.find('.data-table')

    expect(wrapper.find('.el-skeleton').exists()).toBe(true)
    expect(root.attributes('aria-busy')).toBe('true')
    expect(root.classes()).not.toContain('data-table--refreshing')
    expect(wrapper.find('.data-table__count').text()).toBe('')

    await resolve(page(3))
    expect(wrapper.find('.el-skeleton').exists()).toBe(false)
    expect(root.attributes('aria-busy')).not.toBe('true')
    expect(wrapper.find('.data-table__count').text()).toBe(tp('table.results', 3))

    table.columnFilter('name').value = 'ada'
    table.onFilter()
    await flushPromises()

    expect(root.classes()).toContain('data-table--refreshing')
    expect(root.attributes('aria-busy')).toBe('true')
    expect(wrapper.find('.el-skeleton').exists()).toBe(false)
    expect(wrapper.findAll('.el-table__body tr')).toHaveLength(1)

    await resolve(page(1))
    expect(root.classes()).not.toContain('data-table--refreshing')
  })

  it('hides the paginator while a single page holds every row', async () => {
    const single = await renderTable(() => Promise.resolve(page(25)))
    const paged = await renderTable(() => Promise.resolve(page(60)))

    expect(single.wrapper.find('.data-table__footer').isVisible()).toBe(false)
    expect(single.wrapper.findComponent(ElPagination).exists()).toBe(true)
    expect(paged.wrapper.find('.data-table__footer').isVisible()).toBe(true)
    expect(paged.wrapper.findComponent(ElPagination).props('layout')).toBe(
      'sizes, prev, pager, next',
    )
  })

  it('drops the card frame in the flush variant', async () => {
    const card = await renderTable(() => Promise.resolve(page(1)))
    const flush = await renderTable(() => Promise.resolve(page(1)), {
      props: () => ({ variant: 'flush' }),
    })

    expect(card.wrapper.find('.data-table').classes()).not.toContain('data-table--flush')
    expect(flush.wrapper.find('.data-table').classes()).toContain('data-table--flush')
  })

  it('renders the toolbar filters and actions slots', async () => {
    const plain = await renderTable(() => Promise.resolve(page(1)))
    const slotted = await renderTable(() => Promise.resolve(page(1)), {
      slots: () => ({
        filters: () => h('button', { type: 'button', class: 'pill' }, 'Admin'),
        actions: () => h('button', { type: 'button', class: 'export' }, 'Export'),
      }),
    })

    expect(plain.wrapper.find('.data-table__filters').exists()).toBe(false)
    expect(plain.wrapper.find('.data-table__search').exists()).toBe(false)
    expect(slotted.wrapper.find('.data-table__filters .pill').exists()).toBe(true)
    expect(slotted.wrapper.find('.data-table__end .export').exists()).toBe(true)
  })

  it('lists toolbar filter chips before header filter chips', async () => {
    const { wrapper, table } = await renderTable(() => Promise.resolve(page(3)), {
      slots: (rows) => ({
        filters: () =>
          h(ColumnFilterSelect, {
            modelValue: rows.filterValue('name') as string | null,
            label: 'Name',
            options: [{ label: 'Ada Lovelace', value: 'ada' }],
            variant: 'toolbar',
            'onUpdate:modelValue': (value: string | boolean | null) => {
              rows.columnFilter('name').value = value
            },
            onApply: rows.onFilter,
          }),
      }),
    })

    table.columnFilter('email').value = 'ada@'
    table.columnFilter('name').value = 'ada'
    table.onFilter()
    await flushPromises()

    expect(wrapper.find('.data-table__filters .column-filter__pill--active').exists()).toBe(true)
    expect(chipLabels(wrapper.element)).toEqual([
      t('table.filterChip', { label: 'Name', value: 'Ada Lovelace' }),
      t('table.filterChip', { label: 'Email', value: 'ada@' }),
    ])
  })

  it('mirrors a sort chosen by code in the header caret', async () => {
    const fetchPage = vi.fn<FetchPage>(() => Promise.resolve(page(3)))
    const { wrapper, table } = await renderTable(fetchPage)
    const nameHeader = () => wrapper.findAll('.el-table__header th')[0]

    table.setSort('name', -1)

    await vi.waitFor(() => expect(nameHeader()?.classes()).toContain('descending'))
    await vi.waitFor(() =>
      expect(fetchPage).toHaveBeenLastCalledWith({ page: 1, pageSize: 25, sort: '-name' }),
    )

    table.setSort(undefined)

    await vi.waitFor(() => expect(nameHeader()?.classes()).not.toContain('descending'))
    expect(table.sortParam.value).toBeUndefined()
  })

  it('sorts from a header click without fighting the table state', async () => {
    const fetchPage = vi.fn<FetchPage>(() => Promise.resolve(page(3)))
    const { wrapper, table } = await renderTable(fetchPage)
    const nameHeader = () => wrapper.findAll('.el-table__header th')[0]

    await nameHeader()?.trigger('click')

    await vi.waitFor(() =>
      expect(fetchPage).toHaveBeenLastCalledWith({ page: 1, pageSize: 25, sort: 'name' }),
    )
    expect(nameHeader()?.classes()).toContain('ascending')
    expect(table.sortParam.value).toBe('name')

    table.setSort('name', -1)

    await vi.waitFor(() => expect(nameHeader()?.classes()).toContain('descending'))
    expect(table.sortParam.value).toBe('-name')

    table.setSort('name')

    await vi.waitFor(() => expect(nameHeader()?.classes()).toContain('ascending'))
    expect(table.sortParam.value).toBe('name')
  })
})
