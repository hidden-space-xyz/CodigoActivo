import { computed } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  termsDocumentList,
  termsDocumentMutations,
  type TermsDocument,
  type TermsDocumentInput,
} from '@/entities/terms-document'
import { useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Terms document catalog: a table sorted and filterable by name, the create/edit dialog and
 * deletion after confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function useTermsDocumentsAdmin() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

  const table = useServerTable({
    ...termsDocumentList,
    defaultSort: { field: 'name', order: 1 },
    columns: {
      name: { type: 'text' },
    },
  })
  const dialog = useCrudDialog<TermsDocument>()
  const create = useMutation(termsDocumentMutations.create())
  const update = useMutation(termsDocumentMutations.update())
  const remove = useMutation(termsDocumentMutations.remove())
  const saving = computed(() => create.isPending.value || update.isPending.value)

  function save(input: TermsDocumentInput): void {
    const termsDocument = dialog.editing.value
    if (termsDocument) {
      update.mutate(
        { id: termsDocument.id, input },
        feedback.outcome(t('pages.admin.catalogs.termsDocuments.toasts.updated'), dialog.close),
      )
      return
    }
    create.mutate(
      input,
      feedback.outcome(t('pages.admin.catalogs.termsDocuments.toasts.created'), dialog.close),
    )
  }

  function confirmRemove(termsDocument: TermsDocument): void {
    confirmDelete({
      header: t('pages.admin.catalogs.termsDocuments.delete.header'),
      message: t('pages.admin.catalogs.termsDocuments.delete.message', {
        name: termsDocument.name,
      }),
      accept: () => {
        remove.mutate(
          termsDocument.id,
          feedback.outcome(t('pages.admin.catalogs.termsDocuments.toasts.deleted')),
        )
      },
    })
  }

  return { table, dialog, saving, save, confirmRemove }
}
