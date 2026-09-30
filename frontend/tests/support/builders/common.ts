/** Rich-text document with a single paragraph of `text`, as stored by the editor. */
export function richText(text: string): string {
  return JSON.stringify({
    type: 'doc',
    content: [{ type: 'paragraph', content: [{ type: 'text', text }] }],
  })
}

/** Shallow copy of `value` without `keys`, for payloads where a field is absent, not undefined. */
export function omit<T extends object, K extends keyof T>(value: T, ...keys: K[]): Omit<T, K> {
  const copy = { ...value }
  for (const key of keys) delete copy[key]
  return copy
}

/** Audit fields every persisted resource carries; `updatedAt`/`updatedBy` stay unset. */
export const AUDIT = {
  createdAt: '2026-01-01T10:00:00Z',
  createdBy: 'admin-1',
} as const

/** Far-away timestamps so time-dependent behavior does not depend on the day the suite runs. */
export const LONG_AGO = '2020-01-01T09:00:00Z'
export const FAR_FUTURE = '2099-06-01T09:00:00Z'
