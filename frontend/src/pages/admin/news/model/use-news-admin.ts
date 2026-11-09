import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  newsList,
  newsMutations,
  newsQueries,
  type NewsItem,
  type NewsSummary,
} from '@/entities/news-item'
import { useCrudActions, useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Admin news list: a table of the newest news items, searchable by title or subtitle and filtered
 * by creation date, the create/edit dialog, which loads the whole news item before editing it,
 * featuring one news item and deletion after confirmation, each outcome reported with a toast.
 * Call it in `setup`.
 */
export function useNewsAdmin() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()

  const table = useServerTable({
    ...newsList,
    defaultSort: { field: 'createdAt', order: -1 },
    columns: {
      search: { type: 'text' },
      created: { type: 'dateRange', fromParam: 'createdFrom', toParam: 'createdTo' },
    },
  })
  const dialog = useCrudDialog<NewsSummary, NewsItem>({
    load: (row) => queryClient.fetchQuery({ ...newsQueries.detail(row.id), staleTime: 0 }),
    onMissing: () => {
      feedback.error(t('pages.admin.news.toasts.notFound'))
    },
    onError: (error) => {
      feedback.error(error)
    },
  })
  const actions = useCrudActions({
    dialog,
    create: useMutation(newsMutations.create()),
    update: useMutation(newsMutations.update()),
    remove: useMutation(newsMutations.remove()),
    messages: {
      created: t('pages.admin.news.toasts.created'),
      updated: t('pages.admin.news.toasts.saved'),
      deleted: t('pages.admin.news.toasts.deleted'),
      deleteHeader: t('pages.admin.news.delete.header'),
      deleteMessage: (newsItem: NewsSummary) =>
        t('pages.admin.news.delete.message', { title: newsItem.title }),
    },
  })
  const featuring = useMutation(newsMutations.feature())

  function feature(newsItem: NewsSummary): void {
    if (newsItem.featured) return
    featuring.mutate(newsItem.id, feedback.outcome(t('pages.admin.news.toasts.featured')))
  }

  return { table, dialog, ...actions, featuring: featuring.isPending, feature }
}
