import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  termsDocumentList,
  termsDocumentMutations,
  type TermsDocument,
} from '@/entities/terms-document'
import { useCrudActions, useCrudDialog } from '@/shared/lib/crud'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Terms document catalog: a table sorted and filterable by name, the create/edit dialog and
 * deletion after confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function useTermsDocumentsAdmin() {
  const { t } = useI18n()

  const table = useServerTable({
    ...termsDocumentList,
    defaultSort: { field: 'name', order: 1 },
    columns: {
      name: { type: 'text' },
    },
  })
  const dialog = useCrudDialog<TermsDocument>()
  const actions = useCrudActions({
    dialog,
    create: useMutation(termsDocumentMutations.create()),
    update: useMutation(termsDocumentMutations.update()),
    remove: useMutation(termsDocumentMutations.remove()),
    messages: {
      created: t('pages.admin.catalogs.termsDocuments.toasts.created'),
      updated: t('pages.admin.catalogs.termsDocuments.toasts.updated'),
      deleted: t('pages.admin.catalogs.termsDocuments.toasts.deleted'),
      deleteHeader: t('pages.admin.catalogs.termsDocuments.delete.header'),
      deleteMessage: (termsDocument: TermsDocument) =>
        t('pages.admin.catalogs.termsDocuments.delete.message', { name: termsDocument.name }),
    },
  })

  return { table, dialog, ...actions }
}
