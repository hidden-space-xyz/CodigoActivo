import { describe, expect, it } from 'vitest'

import { formatBucketLabel } from '@/pages/admin/dashboard/lib/bucket-label'

describe('formatBucketLabel', () => {
  it('shows month and year for monthly buckets and day and month otherwise', () => {
    expect(formatBucketLabel('2025-01-15', 'month')).toBe('ene 25')
    expect(formatBucketLabel('2025-01-15', 'day')).toBe('15 ene')
  })

  it('returns unparseable input unchanged', () => {
    expect(formatBucketLabel('week-3', 'week')).toBe('week-3')
  })
})
