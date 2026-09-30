import { describe, expect, it } from 'vitest'

import { toSelectOptions } from '@/shared/ui/column-filter'

describe('toSelectOptions', () => {
  it('maps catalog items to options', () => {
    expect(
      toSelectOptions([
        { id: 'a', name: 'Alpha' },
        { id: 'b', name: 'Beta' },
      ]),
    ).toEqual([
      { label: 'Alpha', value: 'a' },
      { label: 'Beta', value: 'b' },
    ])
  })

  it('has no options while the catalog is not loaded', () => {
    expect(toSelectOptions()).toEqual([])
  })
})
