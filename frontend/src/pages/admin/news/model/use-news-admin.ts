import { computed } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  newsList,
  newsMutations,
  newsQueries,
  type NewsItem,
  type NewsItemInput,
  type NewsSummary,
} from '@/entities/news-item'
import { useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Admin news list: a table of the newest news items with a filter per column, the create/edit
 * dialog, which loads the whole news item before editing it, featuring one news item and deletion
 * after confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function useNewsAdmin() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

  const table = useServerTable({
    ...newsList,
    defaultSort: { field: 'createdAt', order: -1 },
    columns: {
      title: { type: 'text' },
      subtitle: { type: 'text' },
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
  const create = useMutation(newsMutations.create())
  const update = useMutation(newsMutations.update())
  const remove = useMutation(newsMutations.remove())
  const featuring = useMutation(newsMutations.feature())
  const saving = computed(() => create.isPending.value || update.isPending.value)

  function save(input: NewsItemInput): void {
    const newsItem = dialog.editing.value
    if (newsItem) {
      update.mutate(
        { id: newsItem.id, input },
        feedback.outcome(t('pages.admin.news.toasts.saved'), dialog.close),
      )
      return
    }
    create.mutate(input, feedback.outcome(t('pages.admin.news.toasts.created'), dialog.close))
  }

  function feature(newsItem: NewsSummary): void {
    if (newsItem.featured) return
    featuring.mutate(newsItem.id, feedback.outcome(t('pages.admin.news.toasts.featured')))
  }

  function confirmRemove(newsItem: NewsSummary): void {
    confirmDelete({
      header: t('pages.admin.news.delete.header'),
      message: t('pages.admin.news.delete.message', { title: newsItem.title }),
      accept: () => {
        remove.mutate(newsItem.id, feedback.outcome(t('pages.admin.news.toasts.deleted')))
      },
    })
  }

  return {
    table,
    dialog,
    saving,
    featuring: featuring.isPending,
    save,
    feature,
    confirmRemove,
  }
}
