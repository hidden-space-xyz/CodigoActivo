import { defineComponent, h } from 'vue'
import type { QueryClient } from '@tanstack/vue-query'

import { createTestQueryClient, renderWithProviders } from '../../render'

/**
 * Runs a composable inside the setup of a tiny host component mounted with every app provider
 * (i18n, Element Plus, TanStack Query, router), so `useQuery`, `useI18n` and friends work. Returns
 * the composable result together with the wrapper and query client.
 */
export async function renderComposable<T>(
  composable: () => T,
  options: { queryClient?: QueryClient } = {},
) {
  let result: T | undefined
  const Host = defineComponent({
    setup() {
      result = composable()
      return () => h('div')
    },
  })

  const queryClient = options.queryClient ?? createTestQueryClient()
  const { wrapper } = await renderWithProviders(Host, { queryClient })
  if (result === undefined) throw new Error('Composable did not run')

  return { result, wrapper, queryClient }
}
