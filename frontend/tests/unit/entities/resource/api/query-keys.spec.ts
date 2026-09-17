import { describe, expect, it } from 'vitest'

import { resourceQueryKeys } from '@/entities/resource'

describe('resourceQueryKeys', () => {
  it('keys the public list by search and shares the resources root', () => {
    expect(resourceQueryKeys.all).toEqual(['resources'])
    expect(resourceQueryKeys.list('')).toEqual(['resources', 'list', ''])
    expect(resourceQueryKeys.list('python')).toEqual(['resources', 'list', 'python'])
    expect(resourceQueryKeys.detail('r1')).toEqual(['resources', 'detail', 'r1'])
    expect(resourceQueryKeys.adminTable()).toEqual(['resources', 'admin'])
  })
})
