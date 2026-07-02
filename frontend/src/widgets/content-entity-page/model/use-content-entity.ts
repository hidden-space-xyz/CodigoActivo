import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useODataTable, type ODataColumn } from '@/shared/lib'

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
  resource: string
  queryKey: readonly unknown[]
  columns: Record<string, ODataColumn>
  defaultSort?: { readonly field: string; readonly order?: 1 | -1 }
  fetchOne: (id: string) => Promise<ContentItem>
  create: (body: ContentRequest) => Promise<unknown>
  update: (id: string, body: ContentRequest) => Promise<unknown>
  remove: (id: string) => Promise<unknown>
  feature?: (id: string) => Promise<unknown>
}

export function useContentEntity(api: ContentApi) {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: api.queryKey })

  const table = useODataTable<ContentItem>({
    resource: api.resource,
    queryKey: api.queryKey,
    columns: api.columns,
    defaultSort: api.defaultSort,
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
    table,
    create,
    update,
    remove,
    feature,
    canFeature: !!api.feature,
    fetchOne: api.fetchOne,
  }
}

export type ContentController = ReturnType<typeof useContentEntity>
