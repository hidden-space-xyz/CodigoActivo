import { createQueryClient, isSessionLost } from '@/shared/api'

/**
 * Application TanStack Query client. Queries are never considered fresh and are dropped as soon as
 * no view observes them, so every mount, focus or reconnect refetches from the API; a failed query
 * is retried once unless the session was lost. Mutations refresh what they declare in
 * `meta.invalidates`.
 */
export const queryClient = createQueryClient({
  defaultOptions: {
    queries: {
      // The API owns response caching. Keep data only while it is being observed by a view.
      staleTime: 0,
      gcTime: 0,
      retry: (failureCount, error) => failureCount < 1 && !isSessionLost(error),
      refetchOnMount: 'always',
      refetchOnWindowFocus: true,
      refetchOnReconnect: 'always',
    },
  },
})
