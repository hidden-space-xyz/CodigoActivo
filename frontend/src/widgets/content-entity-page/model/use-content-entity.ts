import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

export interface ContentItem {
  id?: string
  title?: string | null
  subtitle?: string | null
  description?: string | null
  thumbnailId?: string
  createdAt?: string
  updatedAt?: string | null
  featured?: boolean
}

export interface ContentRequest {
  title: string
  subtitle: string
  description?: string | null
  thumbnailId?: string
}

interface ContentApi {
  queryKey: readonly unknown[]
  fetchAll: (signal: AbortSignal) => Promise<ContentItem[]>
  fetchOne: (id: string) => Promise<ContentItem>
  create: (body: ContentRequest) => Promise<unknown>
  update: (id: string, body: ContentRequest) => Promise<unknown>
  remove: (id: string) => Promise<unknown>
  feature?: (id: string) => Promise<unknown>
}

export function useContentEntity(api: ContentApi) {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: api.queryKey })

  const list = useQuery({
    queryKey: api.queryKey,
    queryFn: ({ signal }) => api.fetchAll(signal),
  })

  const create = useMutation({
    mutationFn: (body: ContentRequest) => api.create(body),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: ContentRequest }) => api.update(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({ mutationFn: (id: string) => api.remove(id), onSuccess: invalidate })

  const feature = useMutation({
    mutationFn: (id: string) => api.feature?.(id) ?? Promise.resolve(),
    onSuccess: invalidate,
  })

  return {
    list,
    create,
    update,
    remove,
    feature,
    canFeature: !!api.feature,
    fetchOne: api.fetchOne,
  }
}

export type ContentController = ReturnType<typeof useContentEntity>
