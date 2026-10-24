import type { VueWrapper } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { NewsPage } from '@/pages/admin/news'
import type { NewsItemResponse, NewsListItemResponse } from '@/shared/api/generated/models'

import { renderApp, renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildNewsItemResponse, omit, richText } from '../../../../../support/builders'
import {
  acceptMessageBox,
  click,
  dismissDialog,
  expectNotification,
  findButton,
  findButtons,
  inputValue,
  isDialogOpen,
  openDialog,
  openRowMenu,
  pickFiles,
  queryOf,
  rowMenuItem,
  rowMenuLabels,
  textOf,
  tp,
  typeInto,
} from '../../../../../support/dom'
import { EMPTY_DOC_JSON } from '@/shared/lib/rich-text'
import { ColumnFilterDate, ColumnSearch } from '@/shared/ui/column-filter'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'
import { SearchInput } from '@/shared/ui/search-input'

const NEW_LABEL = 'pages.admin.news.newLabel'

function buildAdminNewsItem(overrides: Partial<NewsItemResponse> = {}): NewsItemResponse {
  return buildNewsItemResponse({
    title: 'Hackathon',
    subtitle: 'Join us',
    description: richText('Details'),
    createdAt: '2025-04-01T08:00:00Z',
    thumbnailId: 'thumb-news-item-1',
    ...overrides,
  })
}

function editTitle(): string {
  return t('pages.admin.news.form.editHeader')
}

function listItem(overrides: Partial<NewsListItemResponse> = {}): NewsListItemResponse {
  return { ...omit(buildAdminNewsItem(), 'description'), ...overrides }
}

function serveNews(items: NewsListItemResponse[] = [listItem()]) {
  const urls: string[] = []
  server.use(
    http.get('/api/news', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged(items))
    }),
    http.get('/api/files/:id', () => HttpResponse.json({ name: 'cover.png', extension: 'png' })),
  )
  return urls
}

async function renderPage() {
  return renderWithProviders(NewsPage, { attach: true })
}

async function chooseFirstRowAction(wrapper: VueWrapper, label: string): Promise<void> {
  const menu = await openRowMenu(wrapper.find('.el-table__body tr').element)
  await click(rowMenuItem(menu, label))
  await vi.waitFor(() =>
    expect(menu.closest<HTMLElement>('.el-popper')?.style.display).toBe('none'),
  )
}

async function pickThumbnail(dialog: HTMLElement): Promise<void> {
  const input = dialog.querySelector<HTMLInputElement>('.thumb input[type="file"]')
  if (!input) throw new Error('missing thumbnail input')
  await pickFiles(input, [new File(['img'], 'cover.png', { type: 'image/png' })])
}

describe('admin news page', () => {
  it('lists news items newest first and marks the featured one', async () => {
    const urls = serveNews([
      listItem({ featured: true }),
      listItem({ id: 'news-item-2', title: 'Meetup', featured: false }),
    ])

    const { wrapper } = await renderPage()

    expect(wrapper.text()).toContain(t('pages.admin.news.header.title'))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: '-createdAt' })
    expect(textOf(wrapper.find('.data-table__count').element)).toBe(tp('table.results', 2))
    const rows = wrapper.findAll('.el-table__body tr')
    expect(rows).toHaveLength(2)
    const [featured, other] = rows.map((row) => row.element)
    if (!featured || !other) throw new Error('missing rows')
    expect(textOf(featured.querySelector('.table-primary-cell__title'))).toBe('Hackathon')
    expect(textOf(featured.querySelector('.table-primary-cell__subtitle'))).toBe('Join us')
    expect(featured.querySelector('.table-primary-cell__leading .list-thumb--table')).not.toBeNull()
    expect(featured.querySelectorAll('.el-tag')).toHaveLength(1)
    expect(textOf(featured.querySelector('.el-tag'))).toBe(t('pages.admin.news.featured'))
    expect(other.querySelector('.el-tag')).toBeNull()
    expect(findButtons(t('common.edit'), featured)).toHaveLength(1)
    expect(featured.querySelector('.row-actions__more')?.getAttribute('aria-label')).toBe(
      t('table.moreActionsFor', { name: 'Hackathon' }),
    )

    const featuredMenu = await openRowMenu(featured)
    expect(rowMenuLabels(featuredMenu)).toEqual([t('pages.admin.news.feature'), t('common.delete')])
    const alreadyFeatured = rowMenuItem(featuredMenu, t('pages.admin.news.feature'))
    expect(alreadyFeatured.getAttribute('aria-disabled')).toBe('true')
    expect(textOf(alreadyFeatured.querySelector('.row-actions__item-reason'))).toBe(
      t('pages.admin.news.alreadyFeatured'),
    )
    expect(rowMenuItem(featuredMenu, t('common.delete')).classList).toContain(
      'row-actions__item--danger',
    )

    const otherMenu = await openRowMenu(other)
    const feature = rowMenuItem(otherMenu, t('pages.admin.news.feature'))
    expect(feature.getAttribute('aria-disabled')).not.toBe('true')
    expect(feature.querySelector('.row-actions__item-reason')).toBeNull()
  })

  it('renders through the application router for admins', async () => {
    serveNews()

    const { wrapper } = await renderApp('/admin/news', { user: { isAdmin: true } })

    await vi.waitFor(() => expect(wrapper.text()).toContain('Hackathon'))
  })

  it('shows empty and error states', async () => {
    serveNews([])
    const empty = await renderPage()
    expect(empty.wrapper.text()).toContain(t('pages.admin.news.empty.none'))
    empty.wrapper.unmount()

    server.use(http.get('/api/news', () => apiError(500)))
    const failed = await renderPage()
    await vi.waitFor(() =>
      expect(failed.wrapper.text()).toContain(t('pages.admin.news.empty.error')),
    )
  })

  it('sends the title or subtitle search and the creation date filter', async () => {
    const urls = serveNews()
    const { wrapper } = await renderPage()

    expect(wrapper.findAllComponents(ColumnSearch)).toHaveLength(0)
    const search = wrapper.findComponent(SearchInput)
    expect(search.props('label')).toBe(t('common.searchByTitleOrSubtitle'))
    search.vm.$emit('update:modelValue', 'hack')
    wrapper
      .findComponent(ColumnFilterDate)
      .vm.$emit('update:modelValue', [new Date(2025, 2, 1), new Date(2025, 2, 31)])

    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: '-createdAt',
        search: 'hack',
        createdFrom: '2025-03-01',
        createdTo: '2025-03-31',
      }),
    )
    const chips = wrapper.findAll('.filter-chip')
    expect(chips).toHaveLength(1)
    expect(chips[0]?.text()).toContain(t('pages.admin.news.columns.created'))
  })

  it('features a news item and reports failures', async () => {
    serveNews([listItem()])
    const featured: string[] = []
    let fail = false
    server.use(
      http.patch('/api/news/:id/feature', ({ params }) => {
        if (fail) return apiError(500)
        featured.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderPage()

    await chooseFirstRowAction(wrapper, t('pages.admin.news.feature'))
    await expectNotification(t('pages.admin.news.toasts.featured'))
    expect(featured).toEqual(['news-item-1'])

    fail = true
    await chooseFirstRowAction(wrapper, t('pages.admin.news.feature'))
    await expectNotification(t('common.error'))
  })

  it('requires title, subtitle and image before creating', async () => {
    serveNews([])
    const { wrapper } = await renderPage()

    await click(findButton(t(NEW_LABEL)))
    const dialog = openDialog(t(NEW_LABEL))
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('common.imageRequired'))
    expect(dialog.querySelectorAll('.ca-invalid')).toHaveLength(2)
    expect(isDialogOpen(t(NEW_LABEL))).toBe(true)

    await dismissDialog(wrapper, t(NEW_LABEL))
    await vi.waitFor(() => expect(isDialogOpen(t(NEW_LABEL))).toBe(false))
  })

  it('creates a news item with an empty description document', async () => {
    serveNews([])
    let created: unknown
    server.use(
      http.post('/api/files', () => HttpResponse.json({ id: 'uploaded-thumb' })),
      http.post('/api/news', async ({ request }) => {
        created = await request.json()
        return HttpResponse.json(buildAdminNewsItem(), { status: 201 })
      }),
    )
    await renderPage()

    await click(findButton(t(NEW_LABEL)))
    const dialog = openDialog(t(NEW_LABEL))
    await typeInto('#news-title', ' Demo day ')
    await typeInto('#news-subtitle', ' Friday ')
    await pickThumbnail(dialog)
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.news.toasts.created'))
    expect(created).toEqual({
      title: 'Demo day',
      subtitle: 'Friday',
      description: EMPTY_DOC_JSON,
      thumbnailId: 'uploaded-thumb',
    })
    await vi.waitFor(() => expect(isDialogOpen(t(NEW_LABEL))).toBe(false))
  })

  it('shows the upload error when the image cannot be uploaded', async () => {
    serveNews([])
    server.use(http.post('/api/files', () => apiError(500)))
    await renderPage()

    await click(findButton(t(NEW_LABEL)))
    const dialog = openDialog(t(NEW_LABEL))
    await typeInto('#news-title', 'Demo day')
    await typeInto('#news-subtitle', 'Friday')
    await pickThumbnail(dialog)
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(dialog.textContent).toContain(t('common.imageUploadFailed')))
  })

  it('reports a failure creating a news item', async () => {
    serveNews([])
    server.use(
      http.post('/api/files', () => HttpResponse.json({ id: 'uploaded-thumb' })),
      http.post('/api/news', () => apiError(500)),
    )
    await renderPage()

    await click(findButton(t(NEW_LABEL)))
    const dialog = openDialog(t(NEW_LABEL))
    await typeInto('#news-title', 'Demo day')
    await typeInto('#news-subtitle', 'Friday')
    await pickThumbnail(dialog)
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('common.error'))
    expect(isDialogOpen(t(NEW_LABEL))).toBe(true)
  })

  it('loads the news item detail, edits it and saves the description', async () => {
    serveNews()
    let updated: unknown
    server.use(
      http.get('/api/news/:id', () => HttpResponse.json(buildAdminNewsItem())),
      http.put('/api/news/:id', async ({ request, params }) => {
        updated = { id: params.id, body: await request.json() }
        return HttpResponse.json(buildAdminNewsItem())
      }),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('common.edit')))
    const dialog = openDialog(editTitle())
    expect(inputValue('#news-title')).toBe('Hackathon')
    expect(inputValue('#news-subtitle')).toBe('Join us')
    wrapper.findComponent(RichTextEditor).vm.$emit('update:modelValue', richText('New details'))
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.news.toasts.saved'))
    expect(updated).toEqual({
      id: 'news-item-1',
      body: {
        title: 'Hackathon',
        subtitle: 'Join us',
        description: richText('New details'),
        thumbnailId: 'thumb-news-item-1',
      },
    })
    await vi.waitFor(() => expect(isDialogOpen(editTitle())).toBe(false))
  })

  it('reports a failure saving changes', async () => {
    serveNews()
    server.use(
      http.get('/api/news/:id', () => HttpResponse.json(buildAdminNewsItem())),
      http.put('/api/news/:id', () => apiError(500)),
    )
    await renderPage()

    await click(findButton(t('common.edit')))
    await click(findButton(t('common.save'), openDialog(editTitle())))

    await expectNotification(t('common.error'))
    await click(findButton(t('common.cancel'), openDialog(editTitle())))
    await vi.waitFor(() => expect(isDialogOpen(editTitle())).toBe(false))
  })

  it('does not open the editor when the news item no longer exists', async () => {
    serveNews()
    server.use(http.get('/api/news/:id', () => apiError(404)))
    await renderPage()

    await click(findButton(t('common.edit')))

    await vi.waitFor(() =>
      expect(document.body.querySelector('.el-notification--error')).not.toBeNull(),
    )
    expect(isDialogOpen(editTitle())).toBe(false)
  })

  it('reports a missing news item with the not-found message', async () => {
    serveNews()
    server.use(http.get('/api/news/:id', () => apiError(404)))
    await renderPage()

    await click(findButton(t('common.edit')))

    await expectNotification(t('pages.admin.news.toasts.notFound'))
  })

  it('reports a failure loading the news item detail', async () => {
    serveNews()
    server.use(http.get('/api/news/:id', () => apiError(500)))
    await renderPage()

    await click(findButton(t('common.edit')))

    await expectNotification(t('common.error'))
    expect(isDialogOpen(editTitle())).toBe(false)
  })

  it('deletes a news item after confirmation and reports failures', async () => {
    serveNews()
    const deleted: string[] = []
    let fail = false
    server.use(
      http.delete('/api/news/:id', ({ params }) => {
        if (fail) return apiError(500)
        deleted.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderPage()

    await chooseFirstRowAction(wrapper, t('common.delete'))
    const box = document.body.querySelector('.el-message-box')
    expect(box?.textContent).toContain(t('pages.admin.news.delete.header'))
    expect(box?.textContent).toContain('"Hackathon"')
    await acceptMessageBox()
    await expectNotification(t('pages.admin.news.toasts.deleted'))
    expect(deleted).toEqual(['news-item-1'])

    fail = true
    await chooseFirstRowAction(wrapper, t('common.delete'))
    await acceptMessageBox()
    await expectNotification(t('common.error'))
  })
})
