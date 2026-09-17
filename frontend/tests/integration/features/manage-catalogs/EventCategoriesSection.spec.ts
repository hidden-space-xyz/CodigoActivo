import { flushPromises } from '@vue/test-utils'
import { ElColorPicker } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { EventCategoriesSection } from '@/features/manage-catalogs'
import type { EventCategoryTypeResponse } from '@/shared/api/generated/models'
import { ColumnSearch } from '@/shared/ui'

import { buildEventCategory, without } from '../../../support/fixtures/admin-content/builders'
import {
  acceptMessageBox,
  click,
  dismissDialog,
  expectNotification,
  findButton,
  inputValue,
  isDialogOpen,
  openDialog,
  queryOf,
  typeInto,
} from '../../../support/fixtures/admin-content/helpers'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../support/server'

const NEW_TITLE = 'features.manageCatalogs.newHeader'
const EDIT_TITLE = 'features.manageCatalogs.editHeader'

function serveCategories(items: EventCategoryTypeResponse[] = [buildEventCategory()]) {
  const urls: string[] = []
  server.use(
    http.get('/api/events/categoryType', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged(items))
    }),
  )
  return urls
}

async function renderSection() {
  return renderWithProviders(EventCategoriesSection, { attach: true })
}

describe('EventCategoriesSection', () => {
  it('lists categories sorted by name with their color tag', async () => {
    const urls = serveCategories()

    const { wrapper } = await renderSection()

    expect(wrapper.text()).toContain(t('features.manageCatalogs.title'))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'name' })
    expect(wrapper.find('.el-table__body').text()).toContain('Workshop')
  })

  it('shows empty and error states', async () => {
    serveCategories([])
    const empty = await renderSection()
    expect(empty.wrapper.text()).toContain(t('features.manageCatalogs.empty'))
    empty.wrapper.unmount()

    server.use(http.get('/api/events/categoryType', () => apiError(500)))
    const failed = await renderSection()
    await vi.waitFor(() =>
      expect(failed.wrapper.text()).toContain(t('features.manageCatalogs.loadError')),
    )
  })

  it('filters by name and color', async () => {
    const urls = serveCategories()
    const { wrapper } = await renderSection()

    const [name, color] = wrapper.findAllComponents(ColumnSearch)
    name?.vm.$emit('update:modelValue', 'work')
    color?.vm.$emit('update:modelValue', '#FF')
    await flushPromises()

    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ name: 'work', color: '#FF' })
  })

  it('creates a category with the default color and a picked color', async () => {
    serveCategories([])
    const bodies: unknown[] = []
    server.use(
      http.post('/api/events/categoryType', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json(buildEventCategory(), { status: 201 })
      }),
    )
    const { wrapper } = await renderSection()

    await click(findButton(t('features.manageCatalogs.newButton')))
    let dialog = openDialog(t(NEW_TITLE))
    expect(dialog.textContent).toContain('#6366F1')
    expect(dialog.textContent).toContain(t('features.manageCatalogs.example'))
    await typeInto('#event-category-name', '  Talk ')
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('features.manageCatalogs.created'))
    await vi.waitFor(() => expect(isDialogOpen(t(NEW_TITLE))).toBe(false))

    await click(findButton(t('features.manageCatalogs.newButton')))
    dialog = openDialog(t(NEW_TITLE))
    expect(inputValue('#event-category-name')).toBe('')
    await typeInto('#event-category-name', 'Meetup')
    wrapper.findComponent(ElColorPicker).vm.$emit('update:modelValue', '00FF00')
    await flushPromises()
    expect(dialog.textContent).toContain('#00FF00')
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(bodies).toHaveLength(2))
    expect(bodies).toEqual([
      { name: 'Talk', color: '#6366F1' },
      { name: 'Meetup', color: '#00FF00' },
    ])
  })

  it('falls back to the default color when the picker is cleared', async () => {
    serveCategories([buildEventCategory()])
    let body: unknown
    server.use(
      http.put('/api/events/categoryType/:id', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderSection()

    await click(findButton(t('common.edit')))
    const dialog = openDialog(t(EDIT_TITLE))
    expect(inputValue('#event-category-name')).toBe('Workshop')
    wrapper.findComponent(ElColorPicker).vm.$emit('update:modelValue', null)
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('features.manageCatalogs.updated'))
    expect(body).toEqual({ name: 'Workshop', color: '#6366F1' })
  })

  it('requires a name', async () => {
    serveCategories([])
    let posted = false
    server.use(
      http.post('/api/events/categoryType', () => {
        posted = true
        return HttpResponse.json(buildEventCategory())
      }),
    )
    await renderSection()

    await click(findButton(t('features.manageCatalogs.newButton')))
    const dialog = openDialog(t(NEW_TITLE))
    await typeInto('#event-category-name', '   ')
    await click(findButton(t('common.save'), dialog))

    expect(dialog.querySelector('.ca-invalid')).not.toBeNull()
    expect(posted).toBe(false)
    expect(isDialogOpen(t(NEW_TITLE))).toBe(true)
  })

  it('closes the dialog on cancel', async () => {
    serveCategories([])
    const { wrapper } = await renderSection()

    await click(findButton(t('features.manageCatalogs.newButton')))
    await click(findButton(t('common.cancel'), openDialog(t(NEW_TITLE))))
    await vi.waitFor(() => expect(isDialogOpen(t(NEW_TITLE))).toBe(false))

    await click(findButton(t('features.manageCatalogs.newButton')))
    await dismissDialog(wrapper, t(NEW_TITLE))
    await vi.waitFor(() => expect(isDialogOpen(t(NEW_TITLE))).toBe(false))
  })

  it('reports create and update failures', async () => {
    serveCategories([buildEventCategory()])
    server.use(
      http.post('/api/events/categoryType', () => apiError(500)),
      http.put('/api/events/categoryType/:id', () => apiError(500)),
    )
    await renderSection()

    await click(findButton(t('features.manageCatalogs.newButton')))
    await typeInto('#event-category-name', 'Talk')
    await click(findButton(t('common.save'), openDialog(t(NEW_TITLE))))
    await vi.waitFor(() =>
      expect(document.body.querySelectorAll('.el-notification--error')).toHaveLength(1),
    )
    await click(findButton(t('common.cancel'), openDialog(t(NEW_TITLE))))

    await click(findButton(t('common.edit')))
    await click(findButton(t('common.save'), openDialog(t(EDIT_TITLE))))
    await vi.waitFor(() =>
      expect(document.body.querySelectorAll('.el-notification--error')).toHaveLength(2),
    )
    expect(isDialogOpen(t(EDIT_TITLE))).toBe(true)
  })

  it('deletes a category after confirmation', async () => {
    serveCategories([buildEventCategory()])
    const deleted: string[] = []
    server.use(
      http.delete('/api/events/categoryType/:id', ({ params }) => {
        deleted.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderSection()

    await click(findButton(t('common.delete')))
    await acceptMessageBox()

    await expectNotification(t('features.manageCatalogs.deleted'))
    expect(deleted).toEqual(['category-1'])
  })

  it('reports delete failures and ignores rows without id', async () => {
    const ghost = without(buildEventCategory({ name: 'Ghost' }), 'id')
    serveCategories([buildEventCategory(), ghost])
    server.use(http.delete('/api/events/categoryType/:id', () => apiError(500)))
    const { wrapper } = await renderSection()

    const rows = wrapper.findAll('.el-table__body tr')
    await click(findButton(t('common.delete'), rows[1]?.element ?? document.body))
    await acceptMessageBox()
    await flushPromises()
    expect(document.body.querySelectorAll('.el-notification')).toHaveLength(0)

    await click(findButton(t('common.delete'), rows[0]?.element ?? document.body))
    await acceptMessageBox()
    await expectNotification(t('common.error'))
  })
})
