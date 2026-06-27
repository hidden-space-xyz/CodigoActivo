import type { PastEvent } from '@/modules/events/domain/entities/event.entity'

export const ALL_YEARS = 'all' as const

export function selectEventsByYear(
  events: readonly PastEvent[],
  year: string,
): readonly PastEvent[] {
  if (year === ALL_YEARS) return events
  return events.filter((event) => event.year === year)
}

export function availableYears(events: readonly PastEvent[]): readonly string[] {
  const years = new Set(events.map((event) => event.year))
  return [...years].sort((a, b) => Number(b) - Number(a))
}
