import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { FeaturedEventCard } from '@/entities/event'

import { renderWithProviders } from '../../../../support/render'
import { buildEventSummary } from '../../../../support/models'

describe('FeaturedEventCard', () => {
  it('highlights the event with its quoted slogan, date, status, tags and a detail link', async () => {
    const event = buildEventSummary()
    const { wrapper, router } = await renderWithProviders(FeaturedEventCard, { props: { event } })

    expect(wrapper.find('h2').text()).toBe('Día Código Activo')
    expect(wrapper.find('.featured__slogan').text()).toBe('«Programa tu futuro»')
    expect(wrapper.findAll('.el-tag').map((tag) => tag.text())).toEqual(['Robótica', 'IA'])
    expect(wrapper.findAll('.featured__meta-value').map((value) => value.text())).toEqual([
      '3 oct 2026',
      'Inscripción abierta',
    ])
    expect(wrapper.find('img').attributes('src')).toBe('/api/files/thumb-1/content')

    await wrapper.find('a.featured__cta').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/events/event-1')
  })
})
