import { describe, expect, it } from 'vitest'

import { shortTrace } from '@/shared/api'

describe('shortTrace', () => {
  it('keeps the first eight characters of a trace id', () => {
    expect(shortTrace('4bf92f3577b34da6a3ce929d0e0e4736')).toBe('4bf92f35')
  })

  it('keeps an empty trace empty', () => {
    expect(shortTrace('')).toBe('')
  })
})
