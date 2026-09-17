import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { AnnouncementCard } from '@/entities/announcement'

import { buildAnnouncementSummary } from '../../../../support/fixtures/entities/models'
import { renderWithProviders, t } from '../../../../support/render'

describe('AnnouncementCard', () => {
  it('shows the announcement and links to its detail page', async () => {
    const announcement = buildAnnouncementSummary()
    const { wrapper, router } = await renderWithProviders(AnnouncementCard, {
      props: { announcement },
    })

    const link = wrapper.find('a')
    expect(link.attributes('href')).toBe('/announcements/announcement-1')
    expect(wrapper.find('h3').text()).toBe('Abrimos inscripciones')
    expect(wrapper.find('.announcement-card__subtitle').text()).toBe('Plazas limitadas')
    expect(wrapper.find('time').text()).toContain(t('entities.announcement.card.dateLabel'))
    expect(wrapper.find('time').text()).toContain('15 mar 2026')
    expect(wrapper.find('img').attributes()).toMatchObject({
      src: '/api/files/thumb-a/content',
      alt: 'Abrimos inscripciones',
    })

    await link.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('announcement-detail')
    expect(router.currentRoute.value.params).toEqual({ announcementId: 'announcement-1' })
  })

  it('hides the subtitle and the date when they are empty', async () => {
    const announcement = buildAnnouncementSummary({ subtitle: '', date: '' })
    const { wrapper } = await renderWithProviders(AnnouncementCard, { props: { announcement } })

    expect(wrapper.find('.announcement-card__subtitle').exists()).toBe(false)
    expect(wrapper.find('time').exists()).toBe(false)
  })
})
