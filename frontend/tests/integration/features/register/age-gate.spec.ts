import { describe, expect, it } from 'vitest'

import { AgeGate } from '@/features/register'

import { renderWithProviders, t } from '../../../support/render'

function buttonByText(
  wrapper: Awaited<ReturnType<typeof renderWithProviders>>['wrapper'],
  text: string,
) {
  const button = wrapper.findAll('button').find((candidate) => candidate.text() === text)
  if (!button) throw new Error(`button "${text}" not found`)
  return button
}

describe('AgeGate', () => {
  it('asks whether the visitor is an adult', async () => {
    const { wrapper } = await renderWithProviders(AgeGate)

    expect(wrapper.text()).toContain(t('features.register.ageGate.heading'))
    expect(wrapper.text()).toContain(t('features.register.ageGate.question'))
  })

  it('emits confirm when the visitor says they are an adult', async () => {
    const { wrapper } = await renderWithProviders(AgeGate)

    await buttonByText(wrapper, t('features.register.ageGate.confirm')).trigger('click')

    expect(wrapper.emitted('confirm')).toHaveLength(1)
  })

  it('blocks minors with guardian instructions and lets them go back', async () => {
    const { wrapper } = await renderWithProviders(AgeGate)

    await buttonByText(wrapper, t('features.register.ageGate.decline')).trigger('click')

    expect(wrapper.text()).toContain(t('features.register.ageGate.blockedHeading'))
    expect(wrapper.text()).toContain(t('features.register.ageGate.blockedLead'))
    expect(wrapper.text()).not.toContain(t('features.register.ageGate.question'))
    expect(wrapper.emitted('confirm')).toBeUndefined()

    await buttonByText(wrapper, t('features.register.back')).trigger('click')

    expect(wrapper.text()).toContain(t('features.register.ageGate.question'))
  })
})
