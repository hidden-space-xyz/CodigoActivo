import { defineComponent, h } from 'vue'

import { renderWithProviders, type RenderOptions } from '../../render'

/**
 * Runs a composable inside the `setup` of a throwaway component mounted with the app providers, so
 * composables that need i18n, the router or TanStack Query can be tested in isolation.
 */
export async function mountComposable<T>(factory: () => T, options: RenderOptions = {}) {
  let result: T | undefined
  const Host = defineComponent({
    name: 'ComposableHost',
    setup() {
      result = factory()
      return () => h('div')
    },
  })

  const rendered = await renderWithProviders(Host, options)
  if (result === undefined) throw new Error('composable did not run')
  return { ...rendered, result }
}
