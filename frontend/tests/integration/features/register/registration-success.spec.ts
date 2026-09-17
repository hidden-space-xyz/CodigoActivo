import { describe, expect, it } from 'vitest'

import { RegistrationSuccess } from '@/features/register'
import { i18n } from '@/shared/i18n'

import { renderWithProviders, t } from '../../../support/render'

const baseProps = {
  minorCount: 0,
  email: 'ada@example.test',
  isResending: false,
  resendCooldown: 0,
}

function resendButton(wrapper: Awaited<ReturnType<typeof renderWithProviders>>['wrapper']) {
  return wrapper.get('.reg-success__resend-button')
}

describe('RegistrationSuccess', () => {
  it('asks to verify the email and lets the user resend it', async () => {
    const { wrapper } = await renderWithProviders(RegistrationSuccess, { props: baseProps })

    expect(wrapper.text()).toContain(t('features.register.success.title'))
    expect(wrapper.get('.reg-success__verify-intro b').text()).toBe('ada@example.test')
    expect(wrapper.find('.reg-success__role').exists()).toBe(false)
    expect(resendButton(wrapper).text()).toBe(t('features.register.success.resend'))
    expect(resendButton(wrapper).attributes('disabled')).toBeUndefined()

    await resendButton(wrapper).trigger('click')

    expect(wrapper.emitted('resend')).toHaveLength(1)
  })

  it('shows the countdown and disables resending during the cooldown', async () => {
    const { wrapper } = await renderWithProviders(RegistrationSuccess, {
      props: { ...baseProps, resendCooldown: 42 },
    })

    expect(resendButton(wrapper).text()).toBe(
      t('features.register.success.resendCountdown', { s: 42 }),
    )
    expect(resendButton(wrapper).attributes('disabled')).toBeDefined()
  })

  it('disables resending while a resend is in flight', async () => {
    const { wrapper } = await renderWithProviders(RegistrationSuccess, {
      props: { ...baseProps, isResending: true },
    })

    expect(resendButton(wrapper).attributes('disabled')).toBeDefined()
    expect(resendButton(wrapper).attributes('aria-busy')).toBe('true')
  })

  it('pluralizes enrolled minors while still asking to verify the email', async () => {
    const { wrapper } = await renderWithProviders(RegistrationSuccess, {
      props: { ...baseProps, minorCount: 2 },
    })

    expect(wrapper.find('.reg-success__verify').exists()).toBe(true)
    expect(wrapper.get('.reg-success__role b').text()).toBe(
      i18n.global.t('features.register.success.minorsEnrolled', { n: 2 }, 2),
    )
  })

  it('links home and to events and emits reset to register someone else', async () => {
    const { wrapper, router } = await renderWithProviders(RegistrationSuccess, {
      props: { ...baseProps, minorCount: 1 },
    })
    const hrefs = wrapper.findAll('.reg-success__actions a').map((link) => link.attributes('href'))

    expect(hrefs).toEqual([
      router.resolve({ name: 'home' }).href,
      router.resolve({ name: 'events' }).href,
    ])
    expect(wrapper.get('.reg-success__role b').text()).toBe(
      i18n.global.t('features.register.success.minorsEnrolled', { n: 1 }, 1),
    )

    await wrapper.get('.reg-success__again').trigger('click')

    expect(wrapper.emitted('reset')).toHaveLength(1)
  })
})
