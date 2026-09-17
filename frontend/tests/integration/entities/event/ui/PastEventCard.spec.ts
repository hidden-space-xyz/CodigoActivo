import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { PastEventCard } from '@/entities/event'

import { buildPastEvent } from '../../../../support/fixtures/entities/models'
import { renderWithProviders, t } from '../../../../support/render'

describe('PastEventCard', () => {
  it('shows the finished event and links to its detail page', async () => {
    const event = buildPastEvent()
    const { wrapper, router } = await renderWithProviders(PastEventCard, { props: { event } })

    expect(wrapper.find('a').attributes('href')).toBe('/events/event-0')
    expect(wrapper.find('h3').text()).toBe('Edición 2025')
    expect(wrapper.find('.past-card__event').text()).toBe('«Crea y comparte»')
    expect(wrapper.text()).toContain(`${t('entities.event.card.dateLabel')}:`)
    expect(wrapper.text()).toContain('10 may 2025')
    expect(wrapper.findAll('.el-tag').map((tag) => tag.text())).toEqual(['Robótica'])
    expect(wrapper.find('.event-card-footer__status--finished').text()).toBe('Finalizado')

    await wrapper.find('a').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.fullPath).toBe('/events/event-0')
  })

  it('hides the event name and categories when empty', async () => {
    const event = buildPastEvent({ eventName: '', categories: [] })
    const { wrapper } = await renderWithProviders(PastEventCard, { props: { event } })

    expect(wrapper.find('.past-card__event').exists()).toBe(false)
    expect(wrapper.find('.past-card__cats').exists()).toBe(false)
  })
})
