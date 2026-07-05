import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useServerTable, type ServerTableColumn, type ServerTablePage } from '@/shared/lib'

export interface ContentItem {
  id?: string
  title?: string | null
  subtitle?: string | null
  description?: string | null
  thumbnailId?: string
  createdAt?: string
  featured?: boolean
}

export interface ContentRequest {
  title: string
  subtitle: string
  description?: string | null
  thumbnailId?: string
}

interface ContentApi<TParams> {
  queryKey: readonly unknown[]
  fetchPage: (params: TParams) => Promise<ServerTablePage<ContentItem>>
  columns: Record<string, ServerTableColumn>
  defaultSort?: { readonly field: string; readonly order?: 1 | -1 }
  fetchOne: (id: string) => Promise<ContentItem | null>
  create: (body: ContentRequest) => Promise<unknown>
  update: (id: string, body: ContentRequest) => Promise<unknown>
  remove: (id: string) => Promise<unknown>
  feature?: (id: string) => Promise<unknown>
}

export function useContentEntity<TParams = Record<string, unknown>>(api: ContentApi<TParams>) {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: api.queryKey })

  const table = useServerTable<ContentItem, TParams>({
    queryKey: api.queryKey,
    fetchPage: api.fetchPage,
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

  const remove = useMutation({
    mutationFn: (id: string) => api.remove(id),
    onSuccess: invalidate,
  })

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
