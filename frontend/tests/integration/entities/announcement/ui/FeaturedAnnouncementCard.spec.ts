import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { FeaturedAnnouncementCard } from '@/entities/announcement'

import { buildAnnouncementSummary } from '../../../../support/fixtures/entities/models'
import { renderWithProviders, t } from '../../../../support/render'

describe('FeaturedAnnouncementCard', () => {
  it('highlights the announcement with its publication date and a read-more link', async () => {
    const announcement = buildAnnouncementSummary({ featured: true })
    const { wrapper, router } = await renderWithProviders(FeaturedAnnouncementCard, {
      props: { announcement },
    })

    expect(wrapper.find('.featured__badge').text()).toBe(t('entities.announcement.featured.badge'))
    expect(wrapper.find('h2').text()).toBe('Abrimos inscripciones')
    expect(wrapper.find('.featured__slogan').text()).toBe('Plazas limitadas')
    expect(wrapper.find('.featured__cats').exists()).toBe(false)
    expect(wrapper.find('.featured__meta-label').text()).toBe(
      t('entities.announcement.featured.publishedLabel'),
    )
    expect(wrapper.find('.featured__meta-value').text()).toBe('15 mar 2026')

    const cta = wrapper.find('a.featured__cta')
    expect(cta.text()).toBe(t('entities.announcement.featured.readMore'))
    await cta.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/announcements/announcement-1')
  })

  it('hides the date row when the announcement has no date', async () => {
    const announcement = buildAnnouncementSummary({ date: '', subtitle: '' })
    const { wrapper } = await renderWithProviders(FeaturedAnnouncementCard, {
      props: { announcement },
    })

    expect(wrapper.find('.featured__meta').exists()).toBe(false)
    expect(wrapper.find('.featured__slogan').exists()).toBe(false)
  })
})
