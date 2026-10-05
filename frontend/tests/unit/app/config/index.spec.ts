import { VueQueryPlugin } from '@tanstack/vue-query'
import type { App } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import { registerProviders } from '@/app/config'
import { elementPlus } from '@/app/config/element-plus'
import { elementPlusLocale } from '@/app/config/element-plus-locale'
import { queryClient } from '@/app/config/query-client'
import { router } from '@/app/router'
import { ApiError } from '@/shared/api'
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
    const options = elementPlus.options as unknown as { locale: unknown; size: string }

    expect(options.locale).toBe(elementPlusLocale)
    expect(options.size).toBe('default')
  })
})

describe('queryClient', () => {
  it('always refetches and drops unobserved data', () => {
    expect(queryClient.getDefaultOptions().queries).toEqual({
      staleTime: 0,
      gcTime: 0,
      retry: expect.any(Function),
      refetchOnMount: 'always',
      refetchOnWindowFocus: true,
      refetchOnReconnect: 'always',
    })
  })

  it('retries a failed query once unless the session was lost', () => {
    const retry = queryClient.getDefaultOptions().queries?.retry as (
      failureCount: number,
      error: unknown,
    ) => boolean
    const failure = new ApiError(500, 'Error 500')
    const lost = new ApiError(401, 'Error 401', undefined, 'AuthenticationRequired')

    expect(retry(0, failure)).toBe(true)
    expect(retry(1, failure)).toBe(false)
    expect(retry(0, lost)).toBe(false)
  })
})
