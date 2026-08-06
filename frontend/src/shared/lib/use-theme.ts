import { readonly, ref } from 'vue'
import type { Ref } from 'vue'

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'ca-theme'
const DARK_CLASS = 'ca-dark'
const VENDOR_DARK_CLASS = 'dark'

function currentThemeFromDom(): Theme {
  return document.documentElement.classList.contains(DARK_CLASS) ? 'dark' : 'light'
}

const theme = ref<Theme>(currentThemeFromDom())

function syncBrowserChrome(): void {
  const meta = document.querySelector('meta[name="theme-color"]')
  if (!meta) return
  const background = getComputedStyle(document.documentElement)
    .getPropertyValue('--ca-bg')
    .trim()
  if (background) meta.setAttribute('content', background)
}

function apply(next: Theme): void {
  const isDark = next === 'dark'
  document.documentElement.classList.toggle(DARK_CLASS, isDark)
  document.documentElement.classList.toggle(VENDOR_DARK_CLASS, isDark)
  syncBrowserChrome()
  theme.value = next
  try {
    localStorage.setItem(STORAGE_KEY, next)
  } catch {}
}

export function useTheme() {
  const setTheme = (next: Theme): void => apply(next)
  const toggleTheme = (): void => apply(theme.value === 'dark' ? 'light' : 'dark')

  return {
    theme: readonly(theme),
    setTheme,
    toggleTheme,
  }
}

const mediaQueries = new Map<string, Readonly<Ref<boolean>>>()

export function useMediaQuery(query: string): Readonly<Ref<boolean>> {
  const cached = mediaQueries.get(query)
  if (cached) return cached

  const list = window.matchMedia(query)
  const matches = ref(list.matches)
  list.addEventListener('change', (event) => {
    matches.value = event.matches
  })

  const result = readonly(matches)
  mediaQueries.set(query, result)
  return result
}
