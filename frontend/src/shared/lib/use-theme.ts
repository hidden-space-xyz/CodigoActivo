import { readonly, ref } from 'vue'

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'ca-theme'
const DARK_CLASS = 'ca-dark'

function currentThemeFromDom(): Theme {
  return document.documentElement.classList.contains(DARK_CLASS) ? 'dark' : 'light'
}

const theme = ref<Theme>(currentThemeFromDom())

function apply(next: Theme): void {
  document.documentElement.classList.toggle(DARK_CLASS, next === 'dark')
  theme.value = next
  try {
    localStorage.setItem(STORAGE_KEY, next)
  } catch {
    theme.value = next
  }
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
