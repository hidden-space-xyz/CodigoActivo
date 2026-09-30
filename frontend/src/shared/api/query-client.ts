import {
  MutationCache,
  QueryClient,
  type MutationMeta,
  type QueryClientConfig,
  type QueryKey,
} from '@tanstack/vue-query'

declare module '@tanstack/vue-query' {
  interface Register {
    mutationMeta: {
      /** Query keys, usually an entity's `all` key, whose data a successful mutation makes stale. */
      readonly invalidates?: readonly QueryKey[]
    }
  }
}

/**
 * Query client for the application and its tests. Mutations declare in `meta.invalidates` the
 * query keys they make stale; once one succeeds those queries are invalidated and the mutation
 * stays pending until the observed ones have refetched, so a view never shows the data from before
 * the change after the mutation settles.
 */
export function createQueryClient(config: QueryClientConfig = {}): QueryClient {
  const client: QueryClient = new QueryClient({
    ...config,
    mutationCache: new MutationCache({
      onSuccess: async (_data, _variables, _context, mutation) => {
        const keys = mutation.meta?.invalidates ?? []
        await Promise.all(keys.map((queryKey) => client.invalidateQueries({ queryKey })))
      },
    }),
  })
  return client
}

/**
 * Mutation options that also refresh `keys` on success, besides what `options` already declares;
 * for a page whose change affects the data of another entity.
 */
export function alsoInvalidates<TOptions extends { readonly meta?: MutationMeta | undefined }>(
  options: TOptions,
  ...keys: readonly QueryKey[]
): TOptions {
  const invalidates = [...(options.meta?.invalidates ?? []), ...keys]
  return { ...options, meta: { ...options.meta, invalidates } }
}
