import { describe, expect, it } from 'vitest'
import { MutationObserver, QueryObserver } from '@tanstack/vue-query'

import { alsoInvalidates, createQueryClient } from '@/shared/api'

describe('createQueryClient', () => {
  it('refetches the queries a successful mutation declares before the mutation settles', async () => {
    const client = createQueryClient({ defaultOptions: { queries: { retry: false } } })
    let version = 0
    const observer = new QueryObserver(client, {
      queryKey: ['items', 'list'],
      queryFn: () => Promise.resolve(++version),
    })
    const unsubscribe = observer.subscribe(() => undefined)
    await observer.refetch()

    const mutation = new MutationObserver(client, {
      mutationFn: () => Promise.resolve('saved'),
      meta: { invalidates: [['items']] },
    })
    await mutation.mutate()

    expect(client.getQueryData(['items', 'list'])).toBe(2)
    unsubscribe()
  })

  it('leaves the cache alone for mutations without invalidations or that fail', async () => {
    const client = createQueryClient({ defaultOptions: { mutations: { retry: false } } })
    client.setQueryData(['items'], 'kept')

    await new MutationObserver(client, { mutationFn: () => Promise.resolve() }).mutate()
    await expect(
      new MutationObserver(client, {
        mutationFn: () => Promise.reject(new Error('boom')),
        meta: { invalidates: [['items']] },
      }).mutate(),
    ).rejects.toThrow('boom')

    expect(client.getQueryState(['items'])?.isInvalidated).toBe(false)
  })
})

describe('alsoInvalidates', () => {
  it('adds keys to the ones a mutation already declares', () => {
    const options = { mutationFn: () => Promise.resolve(), meta: { invalidates: [['items']] } }

    expect(alsoInvalidates(options, ['other'], ['third']).meta?.invalidates).toEqual([
      ['items'],
      ['other'],
      ['third'],
    ])
    const bare: { readonly meta?: { readonly invalidates?: readonly (readonly unknown[])[] } } = {}
    expect(alsoInvalidates(bare, ['other']).meta?.invalidates).toEqual([['other']])
  })
})
