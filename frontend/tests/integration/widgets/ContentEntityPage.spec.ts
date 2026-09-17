import { defineComponent, h } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { ElButton } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { ContentEntityPage, useContentEntity } from '@/widgets/content-entity-page'
import type { ContentItem } from '@/widgets/content-entity-page/model/use-content-entity'

import {
  acceptMessageBox,
  click,
  findButton,
  inputValue,
  isDialogOpen,
  openDialog,
} from '../../support/fixtures/admin-content/helpers'
import { renderWithProviders, t } from '../../support/render'
import { http, HttpResponse, server } from '../../support/server'

interface FakeApi {
  items: ContentItem[]
  fetchOne?: (id: string) => Promise<ContentItem | null>
  feature?: (id: string) => Promise<unknown>
}

const EDIT_TITLE = () => t('widgets.contentEntityPage.dialog.editHeader', { label: 'post' })

/** Mounts the widget with an in-memory controller, so it is tested apart from any entity API. */
async function renderPage(api: FakeApi) {
  const calls = {
    fetchOne: vi.fn(api.fetchOne ?? (() => Promise.resolve(null))),
    create: vi.fn(() => Promise.resolve()),
    update: vi.fn(() => Promise.resolve()),
    remove: vi.fn(() => Promise.resolve()),
  }
  const Host = defineComponent({
    setup() {
      const controller = useContentEntity({
        queryKey: ['test-content'],
        fetchPage: () => Promise.resolve({ items: api.items, total: api.items.length }),
        columns: { title: { type: 'text' } },
        fetchOne: calls.fetchOne,
        create: calls.create,
        update: calls.update,
        remove: calls.remove,
        ...(api.feature ? { feature: api.feature } : {}),
      })
      return () =>
        h(ContentEntityPage, {
          title: 'Posts',
          subtitle: 'All posts',
          newLabel: 'New post',
          entityLabel: 'post',
          controller,
        })
    },
  })
  server.use(
    http.get('/api/files/:id', () => HttpResponse.json({ name: 'cover', extension: '.png' })),
  )
  const rendered = await renderWithProviders(Host, { attach: true })
  return { ...rendered, calls }
}

describe('ContentEntityPage', () => {
  it('hides feature controls when the entity cannot be featured', async () => {
    const { wrapper } = await renderPage({
      items: [{ id: 'post-1', title: 'First', subtitle: 'Sub', featured: true }],
    })

    expect(wrapper.text()).toContain('Posts')
    expect(wrapper.text()).toContain('First')
    expect(wrapper.find('.el-tag').exists()).toBe(false)
    expect(
      wrapper.find(`button[aria-label="${t('widgets.contentEntityPage.featured')}"]`).exists(),
    ).toBe(false)
  })

  it('edits a row without id directly from the table data', async () => {
    const { calls } = await renderPage({
      items: [{ title: 'Draft', subtitle: 'Unsaved', thumbnailId: 'thumb-1' }],
    })

    await click(findButton(t('common.edit')))

    await vi.waitFor(() => expect(isDialogOpen(EDIT_TITLE())).toBe(true))
    expect(inputValue('#content-entity-title')).toBe('Draft')
    expect(calls.fetchOne).not.toHaveBeenCalled()

    // Without an id the dialog cannot update, so saving creates a new record.
    await click(findButton(t('common.save'), openDialog(EDIT_TITLE())))
    await vi.waitFor(() => expect(calls.create).toHaveBeenCalledTimes(1))
    expect(calls.update).not.toHaveBeenCalled()
  })

  it('ignores delete confirmations for rows without id', async () => {
    const { calls } = await renderPage({ items: [{ title: 'Draft', subtitle: 'Unsaved' }] })

    await click(findButton(t('common.delete')))
    await acceptMessageBox()
    await flushPromises()

    expect(calls.remove).not.toHaveBeenCalled()
  })

  it('does not open the create dialog while a detail is loading', async () => {
    let resolveDetail: (item: ContentItem | null) => void = () => undefined
    const { wrapper, calls } = await renderPage({
      items: [{ id: 'post-1', title: 'First', subtitle: 'Sub' }],
      fetchOne: () =>
        new Promise((resolve) => {
          resolveDetail = resolve
        }),
    })

    await click(findButton(t('common.edit')))
    expect(calls.fetchOne).toHaveBeenCalledWith('post-1')

    // The button is disabled while loading; emit directly to exercise the handler's own guard.
    const create = wrapper
      .findAllComponents(ElButton)
      .find((button) => button.text() === 'New post')
    create?.vm.$emit('click', new MouseEvent('click'))
    await flushPromises()
    expect(isDialogOpen('New post')).toBe(false)

    resolveDetail({ id: 'post-1', title: 'First', subtitle: 'Sub', thumbnailId: 'thumb-1' })
    await vi.waitFor(() => expect(isDialogOpen(EDIT_TITLE())).toBe(true))
  })

  it('ignores feature clicks on already featured or id-less rows', async () => {
    const feature = vi.fn(() => Promise.resolve())
    const { wrapper } = await renderPage({
      items: [
        { id: 'post-1', title: 'Top', subtitle: 'Sub', featured: true },
        { title: 'Draft', subtitle: 'Sub', featured: false },
      ],
      feature,
    })

    const starButtons = wrapper
      .findAllComponents(ElButton)
      .filter((button) =>
        [t('widgets.contentEntityPage.featured'), t('widgets.contentEntityPage.feature')].includes(
          button.attributes('aria-label') ?? '',
        ),
      )
    expect(starButtons).toHaveLength(2)
    // The featured star is disabled; emit directly to exercise the handler's own guard.
    for (const button of starButtons) button.vm.$emit('click', new MouseEvent('click'))
    await flushPromises()

    expect(feature).not.toHaveBeenCalled()
  })
})
