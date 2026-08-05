import { readonly, ref } from 'vue'

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'ca-theme'
const DARK_CLASS = 'ca-dark'
const VENDOR_DARK_CLASS = 'dark'

function currentThemeFromDom(): Theme {
  return document.documentElement.classList.contains(DARK_CLASS) ? 'dark' : 'light'
}

const theme = ref<Theme>(currentThemeFromDom())

function apply(next: Theme): void {
  const isDark = next === 'dark'
  document.documentElement.classList.toggle(DARK_CLASS, isDark)
  document.documentElement.classList.toggle(VENDOR_DARK_CLASS, isDark)
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
