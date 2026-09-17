import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { EventCard } from '@/entities/event'

import { buildUpcomingEvent } from '../../../../support/fixtures/entities/models'
import { renderWithProviders, t } from '../../../../support/render'

describe('EventCard', () => {
  it('shows the event data and links to its detail page', async () => {
    const event = buildUpcomingEvent()
    const { wrapper, router } = await renderWithProviders(EventCard, { props: { event } })

    const link = wrapper.find('a')
    expect(link.attributes('href')).toBe('/events/event-1')
    expect(wrapper.find('h3').text()).toBe('Día Código Activo')
    expect(wrapper.text()).toContain('«Programa tu futuro»')
    expect(wrapper.text()).toContain(`${t('entities.event.card.dateLabel')}:`)
    expect(wrapper.text()).toContain('3 oct 2026')
    expect(wrapper.find('img').attributes()).toMatchObject({
      src: '/api/files/thumb-1/content',
      alt: 'Día Código Activo',
    })
    expect(wrapper.findAll('.el-tag').map((tag) => tag.text())).toEqual(['Robótica', 'IA'])

    const status = wrapper.find('.event-card-footer__status')
    expect(status.text()).toBe('Inscripción abierta')
    expect(status.classes()).toContain('event-card-footer__status--signupOpen')

    await link.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('event-detail')
    expect(router.currentRoute.value.params).toEqual({ eventId: 'event-1' })
  })

  it('hides the slogan, categories, date and image when they are empty', async () => {
    const event = buildUpcomingEvent({
      slogan: '',
      categories: [],
      date: '',
      thumbnailId: '',
      status: { kind: 'upcoming', label: 'Próximamente' },
    })
    const { wrapper } = await renderWithProviders(EventCard, { props: { event } })

    expect(wrapper.text()).not.toContain('«')
    expect(wrapper.find('.event-card__cats').exists()).toBe(false)
    expect(wrapper.find('time').exists()).toBe(false)
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('.list-thumb__placeholder').exists()).toBe(true)
    expect(wrapper.find('.event-card-footer__status--upcoming').text()).toBe('Próximamente')
  })
})
