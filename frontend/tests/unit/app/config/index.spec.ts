import { VueQueryPlugin } from '@tanstack/vue-query'
import type { App } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import { registerProviders } from '@/app/config'
import { elementPlus } from '@/app/config/element-plus'
import { queryClient } from '@/app/config/query-client'
import { router } from '@/app/router'
import { i18n } from '@/shared/i18n'

describe('registerProviders', () => {
  it('installs i18n, Element Plus, TanStack Query and the router in order', () => {
    const use = vi.fn()
    const app = { use } as unknown as App
    use.mockReturnValue(app)

    registerProviders(app)

    expect(use.mock.calls).toEqual([
      [i18n],
      [elementPlus.plugin, elementPlus.options],
      [VueQueryPlugin, { queryClient }],
      [router],
    ])
  })
})

describe('elementPlus', () => {
  it('uses the Spanish locale and default size', () => {
    const options = elementPlus.options as unknown as { locale: { name: string }; size: string }

    expect(options.locale.name).toBe('es')
    expect(options.size).toBe('default')
  })
})

describe('queryClient', () => {
  it('always refetches and drops unobserved data', () => {
    expect(queryClient.getDefaultOptions().queries).toEqual({
      staleTime: 0,
      gcTime: 0,
      retry: 1,
      refetchOnMount: 'always',
      refetchOnWindowFocus: true,
      refetchOnReconnect: 'always',
    })
  })
})
