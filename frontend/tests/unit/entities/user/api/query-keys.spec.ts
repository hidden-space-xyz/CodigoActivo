import { describe, expect, it } from 'vitest'

import { userQueryKeys } from '@/entities/user'

describe('userQueryKeys', () => {
  it('nests the admin table under the users root', () => {
    expect(userQueryKeys.all).toEqual(['users'])
    expect(userQueryKeys.adminTable()).toEqual(['users', 'table'])
  })
})
