import { defineComponent, h } from 'vue'

import { renderWithProviders, type RenderOptions } from '../../render'

/**
 * Runs a composable inside the `setup` of a throwaway component mounted with the app providers, so
 * it can use `useI18n`, `useRoute` or TanStack Query. Returns the composable's result together with
 * the wrapper, router and query client.
 */
export async function withSetup<T>(composable: () => T, options: RenderOptions = {}) {
  const box: { value?: T } = {}
  const Harness = defineComponent({
    name: 'ComposableHarness',
    setup() {
      box.value = composable()
      return () => h('div')
    },
  })
  const rendered = await renderWithProviders(Harness, options)
  if (!('value' in box)) throw new Error('Composable did not run')
  return { result: box.value, ...rendered }
}
