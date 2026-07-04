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

interface CatalogApi<TItem, TBody> {
  queryKey: readonly unknown[]
  fetchAll: () => Promise<TItem[]>
  create: (body: TBody) => Promise<unknown>
  update: (id: string, body: TBody) => Promise<unknown>
  remove: (id: string) => Promise<unknown>
}

export function useCatalog<TItem extends CatalogItem = CatalogItem, TBody = CatalogRequest>(
  api: CatalogApi<TItem, TBody>,
) {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: api.queryKey })

  const list = useQuery({
    queryKey: api.queryKey,
    queryFn: () => api.fetchAll(),
  })

  const create = useMutation({ mutationFn: api.create, onSuccess: invalidate })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: TBody }) => api.update(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({ mutationFn: (id: string) => api.remove(id), onSuccess: invalidate })

  return { list, create, update, remove }
}

export type CatalogController = ReturnType<typeof useCatalog<CatalogItem, CatalogRequest>>
