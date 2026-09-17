import { QueryClient } from '@tanstack/vue-query'

/**
 * Shared TanStack Query client. Queries are never considered fresh and are dropped as soon as no
 * view observes them, so every mount, focus or reconnect refetches from the API.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // The API owns response caching. Keep data only while it is being observed by a view.
      staleTime: 0,
      gcTime: 0,
      retry: 1,
      refetchOnMount: 'always',
      refetchOnWindowFocus: true,
      refetchOnReconnect: 'always',
    },
  },
})
