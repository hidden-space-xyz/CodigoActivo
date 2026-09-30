import { readonly, ref } from 'vue'

/** Color scheme applied through the `ca-dark` class on the root element. */
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
  const background = getComputedStyle(document.documentElement).getPropertyValue('--ca-bg').trim()
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

/**
 * App-wide theme state. The initial value comes from the root class set by `public/theme-init.js`;
 * changing it toggles `ca-dark` and Element Plus `dark`, syncs the `theme-color` meta and persists
 * the choice to `localStorage`.
 */
export function useTheme() {
  const setTheme = (next: Theme): void => apply(next)
  const toggleTheme = (): void => apply(theme.value === 'dark' ? 'light' : 'dark')

  return {
    theme: readonly(theme),
    setTheme,
    toggleTheme,
  }
}
