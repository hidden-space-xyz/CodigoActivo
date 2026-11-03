import { describe, expect, it } from 'vitest'

import { useResourcesAdmin } from '@/pages/admin/resources/model/use-resources-admin'

import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import {
  buildResourceListItem,
  buildResourceResponse,
  internalType,
} from '../../../../../support/builders'
import { expectNotification } from '../../../../../support/dom'
import { buildResourceSummary } from '../../../../../support/models'
import { mountComposable, t } from '../../../../../support/render'

function serveList() {
  const urls: string[] = []
  server.use(
    http.get('/api/resources', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged([buildResourceListItem()]))
    }),
    http.get('/api/resources/types', () => HttpResponse.json([internalType])),
  )
  return urls
}

describe('useResourcesAdmin', () => {
  it('loads the whole resource before editing it and reports one that no longer exists', async () => {
    serveList()
    server.use(
      http.get('/api/resources/:id', ({ params }) =>
        params.id === 'missing' ? apiError(404) : HttpResponse.json(buildResourceResponse()),
      ),
    )
    const { result } = await mountComposable(() => useResourcesAdmin())

    await result.dialog.openEdit(buildResourceSummary({ id: 'resource-1' }))
    expect(result.dialog.editing.value?.description).toBe(buildResourceResponse().description)

    result.dialog.close()
    await result.dialog.openEdit(buildResourceSummary({ id: 'missing' }))
    expect(result.dialog.visible.value).toBe(false)
    await expectNotification(t('pages.admin.resources.toasts.notFound'))
  })
})
