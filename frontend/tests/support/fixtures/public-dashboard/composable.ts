import { defineComponent, h } from 'vue'
import type { QueryClient } from '@tanstack/vue-query'

import type { AuthUser } from '@/entities/session/model/types'

import { createTestQueryClient, renderWithProviders } from '../../render'

/**
 * Runs `composable` inside the setup of a host component mounted with every app provider, so
 * `useQuery`, `useQueryClient` and `useI18n` work. Signs in `user` first when given.
 */
export async function mountComposable<T>(
  composable: () => T,
  options: { queryClient?: QueryClient; user?: Partial<AuthUser> } = {},
) {
  let result: T | undefined
  const Host = defineComponent({
    setup() {
      result = composable()
      return () => h('div')
    },
  })

  const queryClient = options.queryClient ?? createTestQueryClient()
  const { wrapper } = await renderWithProviders(Host, {
    queryClient,
    ...(options.user ? { user: options.user } : {}),
  })
  if (result === undefined) throw new Error('Composable did not run')

  return { result, wrapper, queryClient }
}
