import { describe, expect, it } from 'vitest'

import { useTheme } from '@/shared/lib'
import { ThemeToggle } from '@/shared/ui'

import { renderWithProviders, t } from '../../../support/render'

describe('ThemeToggle', () => {
  it('switches between light and dark themes', async () => {
    useTheme().setTheme('light')
    const { wrapper } = await renderWithProviders(ThemeToggle)

    const button = wrapper.find('button')
    expect(button.attributes('aria-label')).toBe(t('theme.toDark'))
    expect(button.attributes('title')).toBe(t('theme.toDark'))
    expect(button.attributes('aria-pressed')).toBe('false')
    expect(button.classes()).not.toContain('theme-toggle--dark')

    await button.trigger('click')

    expect(document.documentElement.classList.contains('ca-dark')).toBe(true)
    expect(button.attributes('aria-label')).toBe(t('theme.toLight'))
    expect(button.attributes('aria-pressed')).toBe('true')
    expect(button.classes()).toContain('theme-toggle--dark')

    await button.trigger('click')

    expect(document.documentElement.classList.contains('ca-dark')).toBe(false)
    expect(button.attributes('aria-label')).toBe(t('theme.toDark'))
  })
})
