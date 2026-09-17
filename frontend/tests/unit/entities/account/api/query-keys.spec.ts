import { describe, expect, it } from 'vitest'

import { accountQueryKeys } from '@/entities/account'

describe('accountQueryKeys', () => {
  it('nests every account key under the shared root', () => {
    expect(accountQueryKeys.all).toEqual(['account'])
    expect(accountQueryKeys.me()).toEqual(['account', 'me'])
    expect(accountQueryKeys.children()).toEqual(['account', 'children'])
    expect(accountQueryKeys.history()).toEqual(['account', 'history'])
    expect(accountQueryKeys.certificates()).toEqual(['account', 'certificates'])
  })
})
