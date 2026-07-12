import { useMutation, useQueryClient } from '@tanstack/vue-query'

interface CatalogApi<TBody> {
  queryKey: readonly unknown[]
  create: (body: TBody) => Promise<unknown>
  update: (id: string, body: TBody) => Promise<unknown>
  remove: (id: string) => Promise<unknown>
}

export function useCatalog<TBody>(api: CatalogApi<TBody>) {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: api.queryKey })

  const create = useMutation({
    mutationFn: (body: TBody) => api.create(body),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: TBody }) => api.update(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({ mutationFn: (id: string) => api.remove(id), onSuccess: invalidate })

  return { create, update, remove }
}
