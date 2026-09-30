import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { NewsCard } from '@/entities/news-item'

import { renderWithProviders, t } from '../../../../support/render'
import { buildNewsSummary } from '../../../../support/models'

describe('NewsCard', () => {
  it('shows the news item and links to its detail page', async () => {
    const newsItem = buildNewsSummary()
    const { wrapper, router } = await renderWithProviders(NewsCard, {
      props: { newsItem },
    })

    const link = wrapper.find('a')
    expect(link.attributes('href')).toBe('/news/news-item-1')
    expect(wrapper.find('h3').text()).toBe('Abrimos inscripciones')
    expect(wrapper.find('.news-card__subtitle').text()).toBe('Plazas limitadas')
    expect(wrapper.find('time').text()).toContain(t('entities.newsItem.card.dateLabel'))
    expect(wrapper.find('time').text()).toContain('15 mar 2026')
    expect(wrapper.find('img').attributes()).toMatchObject({
      src: '/api/files/thumb-a/content',
      alt: 'Abrimos inscripciones',
    })

    await link.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('news-detail')
    expect(router.currentRoute.value.params).toEqual({ newsItemId: 'news-item-1' })
  })

  it('hides the subtitle when the news item has none', async () => {
    const newsItem = buildNewsSummary({ subtitle: '' })
    const { wrapper } = await renderWithProviders(NewsCard, { props: { newsItem } })

    expect(wrapper.find('.news-card__subtitle').exists()).toBe(false)
  })
})
