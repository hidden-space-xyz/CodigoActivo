import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

export interface CatalogItem {
  id?: string
  name?: string | null
  description?: string | null
}

export interface CatalogRequest {
  name?: string | null
  description?: string | null
}

export interface CatalogApi {
  queryKey: readonly unknown[]
  fetchAll: (signal: AbortSignal) => Promise<CatalogItem[]>
  create: (body: CatalogRequest) => Promise<unknown>
  update: (id: string, body: CatalogRequest) => Promise<unknown>
  remove: (id: string) => Promise<unknown>
}

export function useCatalog(api: CatalogApi) {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: api.queryKey })

  const list = useQuery({
    queryKey: api.queryKey,
    queryFn: ({ signal }) => api.fetchAll(signal),
  })

  const create = useMutation({ mutationFn: api.create, onSuccess: invalidate })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: CatalogRequest }) => api.update(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({ mutationFn: (id: string) => api.remove(id), onSuccess: invalidate })

  return { list, create, update, remove }
}

export type CatalogController = ReturnType<typeof useCatalog>
