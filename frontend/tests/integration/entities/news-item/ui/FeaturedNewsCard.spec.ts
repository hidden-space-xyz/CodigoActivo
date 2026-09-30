import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { FeaturedNewsCard } from '@/entities/news-item'

import { renderWithProviders, t } from '../../../../support/render'
import { buildNewsSummary } from '../../../../support/models'

describe('FeaturedNewsCard', () => {
  it('highlights the news item with its publication date and a read-more link', async () => {
    const newsItem = buildNewsSummary({ featured: true })
    const { wrapper, router } = await renderWithProviders(FeaturedNewsCard, {
      props: { newsItem },
    })

    expect(wrapper.find('.featured__badge').text()).toBe(t('entities.newsItem.featured.badge'))
    expect(wrapper.find('h2').text()).toBe('Abrimos inscripciones')
    expect(wrapper.find('.featured__slogan').text()).toBe('Plazas limitadas')
    expect(wrapper.find('.featured__cats').exists()).toBe(false)
    expect(wrapper.find('.featured__meta-label').text()).toBe(
      t('entities.newsItem.featured.publishedLabel'),
    )
    expect(wrapper.find('.featured__meta-value').text()).toBe('15 mar 2026')

    const cta = wrapper.find('a.featured__cta')
    expect(cta.text()).toBe(t('entities.newsItem.featured.readMore'))
    await cta.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/news/news-item-1')
  })

  it('hides the subtitle when the news item has none', async () => {
    const newsItem = buildNewsSummary({ subtitle: '' })
    const { wrapper } = await renderWithProviders(FeaturedNewsCard, {
      props: { newsItem },
    })

    expect(wrapper.find('.featured__slogan').exists()).toBe(false)
  })
})
