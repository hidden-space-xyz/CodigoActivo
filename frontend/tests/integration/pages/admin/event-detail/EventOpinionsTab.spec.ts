import { flushPromises } from '@vue/test-utils'
import { ElPagination, ElRate } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import EventOpinionsTab from '@/pages/admin/event-detail/ui/EventOpinionsTab.vue'
import type { EventRatingListItemResponse } from '@/shared/api/generated/models'
import { i18n } from '@/shared/i18n'

import { buildRating, EVENT_ID } from '../../../../support/fixtures/admin-events/builders'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function tc(key: string, count: number): string {
  return (i18n.global.t as (key: string, plural: number) => string)(key, count)
}

async function renderTab(
  ratings: EventRatingListItemResponse[],
  total = ratings.length,
  urls: URL[] = [],
  active = true,
) {
  server.use(
    http.get('/api/events/:eventId/ratings', ({ request }) => {
      urls.push(new URL(request.url))
      return HttpResponse.json(paged(ratings, total))
    }),
  )
  const rendered = await renderWithProviders(EventOpinionsTab, {
    props: { eventId: EVENT_ID, active },
  })
  await flushPromises()
  return rendered
}

describe('EventOpinionsTab', () => {
  it('lists anonymous ratings with their non-empty answers', async () => {
    const { wrapper } = await renderTab([
      buildRating({
        mostLiked: 'The people',
        leastLiked: '   ',
        suggestions: 'More coffee',
      }),
      buildRating({
        id: 'rating-2',
        score: undefined,
        mostLiked: null,
        leastLiked: null,
        suggestions: null,
      }),
    ])

    await vi.waitFor(() => expect(wrapper.findAll('li.opinion')).toHaveLength(2))
    expect(wrapper.text()).toContain(tc('pages.admin.eventDetail.opinions.count', 2))
    const [first, second] = wrapper.findAll('li.opinion')
    expect(first?.text()).toContain(t('pages.admin.eventDetail.opinions.anonymous'))
    expect(first?.find('.opinion__score').text()).toBe('4/5')
    expect(first?.find('.opinion__date').exists()).toBe(false)
    expect(first?.findAll('dt').map((node) => node.text())).toEqual([
      t('entities.event.ratingQuestions.mostLiked'),
      t('entities.event.ratingQuestions.suggestions'),
    ])
    expect(first?.findAll('dd').map((node) => node.text())).toEqual(['The people', 'More coffee'])
    expect(second?.find('.opinion__score').text()).toBe('0/5')
    expect(second?.text()).toContain(t('pages.admin.eventDetail.opinions.noAnswers'))
    expect(wrapper.findAllComponents(ElRate)[1]?.props('modelValue')).toBe(0)
    expect(wrapper.findComponent(ElPagination).exists()).toBe(false)
  })

  it('shows the empty and error states', async () => {
    const empty = await renderTab([])
    await vi.waitFor(() =>
      expect(empty.wrapper.text()).toContain(t('pages.admin.eventDetail.opinions.empty')),
    )
    empty.wrapper.unmount()

    server.use(http.get('/api/events/:eventId/ratings', () => apiError(500)))
    const failing = await renderWithProviders(EventOpinionsTab, {
      props: { eventId: EVENT_ID, active: true },
    })
    await vi.waitFor(() => expect(failing.wrapper.text()).toContain(t('dataState.error')))
  })

  it('does not load ratings while inactive', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderTab([buildRating()], 1, urls, false)

    expect(urls).toHaveLength(0)
    await wrapper.setProps({ active: true })
    await vi.waitFor(() => expect(urls).toHaveLength(1))
  })

  it('pages through many ratings', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderTab([buildRating()], 30, urls)

    const pagination = wrapper.findComponent(ElPagination)
    expect(pagination.exists()).toBe(true)
    pagination.vm.$emit('update:current-page', 2)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('page')).toBe('2'))
    pagination.vm.$emit('update:page-size', 50)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('pageSize')).toBe('50'))
  })
})
