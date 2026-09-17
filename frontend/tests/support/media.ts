type Listener = (event: MediaQueryListEvent) => void

interface MockMediaQueryList {
  readonly list: MediaQueryList
  readonly listeners: Set<Listener>
  matches: boolean
}

const lists = new Map<string, MockMediaQueryList>()
let predicate: (query: string) => boolean = () => false

/**
 * `window.matchMedia` replacement. Every query starts unmatched; `setMediaQueryMatches` changes the
 * result and notifies listeners, so composables that cache a query at module level still react.
 */
export function matchMediaMock(query: string): MediaQueryList {
  const existing = lists.get(query)
  if (existing) return existing.list

  const listeners = new Set<Listener>()
  const entry: MockMediaQueryList = {
    listeners,
    matches: predicate(query),
    list: {
      get matches() {
        return entry.matches
      },
      media: query,
      onchange: null,
      addListener: (listener: Listener) => listeners.add(listener),
      removeListener: (listener: Listener) => listeners.delete(listener),
      addEventListener: (_type: string, listener: Listener) => listeners.add(listener),
      removeEventListener: (_type: string, listener: Listener) => listeners.delete(listener),
      dispatchEvent: () => false,
    } as unknown as MediaQueryList,
  }
  lists.set(query, entry)
  return entry.list
}

/** Makes queries accepted by `matches` match (e.g. `(q) => q.includes('max-width')`) and notifies. */
export function setMediaQueryMatches(matches: (query: string) => boolean): void {
  predicate = matches
  for (const [query, entry] of lists) {
    const next = matches(query)
    if (next === entry.matches) continue
    entry.matches = next
    const event = { matches: next, media: query } as MediaQueryListEvent
    for (const listener of entry.listeners) listener(event)
  }
}

/** Restores the default of no matching media query. Called after every test. */
export function resetMediaQueries(): void {
  setMediaQueryMatches(() => false)
}
