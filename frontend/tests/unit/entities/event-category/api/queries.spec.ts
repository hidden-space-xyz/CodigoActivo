import { describe, expect, it } from 'vitest'

import {
  eventCategoryKeys,
  eventCategoryList,
  eventCategoryQueries,
} from '@/entities/event-category'

import { http, HttpResponse, paged, server } from '../../../../support/server'
import { buildEventCategoryType } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

describe('event category queries', () => {
  it('nests the options and the admin list under one root', () => {
    expect(eventCategoryKeys.options()).toEqual(['event-categories', 'options'])
    expect(eventCategoryKeys.list()).toEqual(['event-categories', 'list'])
    expect(eventCategoryList.queryKey).toEqual(eventCategoryKeys.list())
  })

  it('loads the options and pages the admin list', async () => {
    server.use(
      http.get('/api/events/categoryType', () =>
        HttpResponse.json(paged([buildEventCategoryType()])),
      ),
    )

    await expect(
      createTestQueryClient().fetchQuery(eventCategoryQueries.options()),
    ).resolves.toEqual([{ id: 'category-1', name: 'Workshop', color: '#FF0000' }])
    await expect(eventCategoryList.fetchPage({ page: 1 })).resolves.toMatchObject({ total: 1 })
  })
})
