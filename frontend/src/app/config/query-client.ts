import { ApiError, createQueryClient, isSessionLost } from '@/shared/api'

const RETRY_DELAY_MS = 1_000
const LONGEST_RATE_LIMIT_WAIT_MS = 5_000

function rateLimitWait(error: unknown): number | undefined {
  return error instanceof ApiError && error.status === 429 ? error.retryAfterMs : undefined
}

/**
 * Application TanStack Query client. Queries are never considered fresh and are dropped as soon as
 * no view observes them, so every mount, focus or reconnect refetches from the API; a failed query
 * is retried once after a second unless the session was lost. A query refused for too many
 * requests waits what its `Retry-After` asks, plus a random share as long again so the clients
 * refused together do not return together, and fails at once when that is longer than five
 * seconds. Mutations refresh what they declare in `meta.invalidates`.
 */
export const queryClient = createQueryClient({
  defaultOptions: {
    queries: {
      staleTime: 0,
      gcTime: 0,
      retry: (failureCount, error) =>
        failureCount < 1 &&
        !isSessionLost(error) &&
        (rateLimitWait(error) ?? 0) <= LONGEST_RATE_LIMIT_WAIT_MS,
      retryDelay: (_failureCount, error) => {
        const wait = rateLimitWait(error)
        return wait === undefined
          ? RETRY_DELAY_MS
          : Math.max(wait, RETRY_DELAY_MS) * (1 + Math.random())
      },
      refetchOnMount: 'always',
      refetchOnWindowFocus: true,
      refetchOnReconnect: 'always',
    },
  },
})
