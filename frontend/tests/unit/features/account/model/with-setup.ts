import { defineComponent, h } from 'vue'

import { renderWithProviders, type RenderOptions } from '../../../../support/render'

/**
 * Runs a composable inside a mounted component with the app providers (query client, router, i18n)
 * and returns its result together with the router and query client used.
 */
export async function withSetup<T>(composable: () => T, options: RenderOptions = {}) {
  let result: T | undefined
  const Host = defineComponent({
    setup() {
      result = composable()
      return () => h('div')
    },
  })
  const rendered = await renderWithProviders(Host, options)
  if (result === undefined) throw new Error('Composable did not run')
  return { ...rendered, result }
}
