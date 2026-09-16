import { QueryClient } from '@tanstack/vue-query'

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
