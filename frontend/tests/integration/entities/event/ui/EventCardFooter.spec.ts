import { describe, expect, it } from 'vitest'

import type { EventStatusKind } from '@/entities/event/model/types'
import EventCardFooter from '@/entities/event/ui/EventCardFooter.vue'

import { renderWithProviders, t } from '../../../../support/render'

describe('EventCardFooter', () => {
  it.each<EventStatusKind>([
    'upcoming',
    'earlySignupOpen',
    'signupOpen',
    'signupClosed',
    'finished',
  ])('renders the %s status label with its color modifier', async (kind) => {
    const label = t(`entities.event.status.${kind}`)
    const { wrapper } = await renderWithProviders(EventCardFooter, {
      props: { status: { kind, label } },
    })

    const status = wrapper.find('.event-card-footer__status')
    expect(status.text()).toBe(label)
    expect(status.classes()).toContain(`event-card-footer__status--${kind}`)
  })
})
