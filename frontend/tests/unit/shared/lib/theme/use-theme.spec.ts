import { describe, expect, it, vi } from 'vitest'

import { useTheme } from '@/shared/lib/theme'

function addThemeColorMeta(): HTMLMetaElement {
  const meta = document.createElement('meta')
  meta.setAttribute('name', 'theme-color')
  meta.setAttribute('content', 'initial')
  document.head.append(meta)
  return meta
}

describe('useTheme', () => {
  it('starts from the theme class set on the root element before the app loads', async () => {
    vi.resetModules()
    document.documentElement.classList.add('ca-dark')

    const module = await import('@/shared/lib/theme')

    expect(module.useTheme().theme.value).toBe('dark')
  })

  it('applies the dark theme classes and persists the choice', () => {
    const { theme, setTheme } = useTheme()

    setTheme('dark')

    expect(theme.value).toBe('dark')
    expect(document.documentElement.classList.contains('ca-dark')).toBe(true)
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(localStorage.getItem('ca-theme')).toBe('dark')

    setTheme('light')

    expect(theme.value).toBe('light')
    expect(document.documentElement.classList.contains('ca-dark')).toBe(false)
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(localStorage.getItem('ca-theme')).toBe('light')
  })

  it('toggles between light and dark', () => {
    const { theme, setTheme, toggleTheme } = useTheme()
    setTheme('light')

    toggleTheme()
    expect(theme.value).toBe('dark')

    toggleTheme()
    expect(theme.value).toBe('light')
  })

  it('shares the theme state between callers', () => {
    const first = useTheme()
    const second = useTheme()

    first.setTheme('dark')

    expect(second.theme.value).toBe('dark')
  })

  it('syncs the theme-color meta with the background variable', () => {
    const meta = addThemeColorMeta()
    document.documentElement.style.setProperty('--ca-bg', ' #101010 ')

    useTheme().setTheme('dark')

    expect(meta.getAttribute('content')).toBe('#101010')
    document.documentElement.style.removeProperty('--ca-bg')
    meta.remove()
  })

  it('leaves the theme-color meta untouched when the background variable is empty', () => {
    const meta = addThemeColorMeta()

    useTheme().setTheme('light')

    expect(meta.getAttribute('content')).toBe('initial')
    meta.remove()
  })

  it('still applies the theme when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('Quota exceeded')
    })
    const { theme, setTheme } = useTheme()

    setTheme('dark')

    expect(theme.value).toBe('dark')
    expect(document.documentElement.classList.contains('ca-dark')).toBe(true)
  })
})
