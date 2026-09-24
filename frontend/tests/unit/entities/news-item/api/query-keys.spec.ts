import { describe, expect, it } from 'vitest'

import { newsQueryKeys } from '@/entities/news-item'

describe('newsQueryKeys', () => {
  it('nests every news key under the shared root', () => {
    expect(newsQueryKeys.all).toEqual(['news'])
    expect(newsQueryKeys.publicDetail('a1')).toEqual(['news', 'public', 'a1'])
    expect(newsQueryKeys.years()).toEqual(['news', 'years'])
    expect(newsQueryKeys.byYear('2026', 'robot')).toEqual(['news', 'year', '2026', 'robot'])
    expect(newsQueryKeys.home()).toEqual(['news', 'home'])
  })
})
