import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { FeaturedEventCard } from '@/entities/event'

import { buildUpcomingEvent } from '../../../../support/fixtures/entities/models'
import { renderWithProviders, t } from '../../../../support/render'

describe('FeaturedEventCard', () => {
  it('highlights the event with its quoted slogan, date, status, tags and a detail link', async () => {
    const event = buildUpcomingEvent()
    const { wrapper, router } = await renderWithProviders(FeaturedEventCard, { props: { event } })

    expect(wrapper.find('.featured__badge').text()).toBe(t('entities.event.featured.badge'))
    expect(wrapper.find('h2').text()).toBe('Día Código Activo')
    expect(wrapper.find('.featured__slogan').text()).toBe('«Programa tu futuro»')
    expect(wrapper.findAll('.el-tag').map((tag) => tag.text())).toEqual(['Robótica', 'IA'])
    expect(
      wrapper
        .findAll('.featured__meta-item')
        .map((item) => [
          item.find('.featured__meta-label').text(),
          item.find('.featured__meta-value').text(),
        ]),
    ).toEqual([
      [t('entities.event.featured.dateLabel'), '3 oct 2026'],
      [t('common.status'), 'Inscripción abierta'],
    ])
    expect(wrapper.find('img').attributes('src')).toBe('/api/files/thumb-1/content')

    const cta = wrapper.find('a.featured__cta')
    expect(cta.text()).toBe(t('entities.event.featured.viewDetails'))
    await cta.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/events/event-1')
  })
})
