import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { partnerList, partnerMutations, type Partner } from '@/entities/partner'
import { useCrudActions, useCrudDialog } from '@/shared/lib/crud'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Admin partner list: a table sorted by tier with a filter per column, the create/edit dialog and
 * deletion after confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function usePartnersAdmin() {
  const { t } = useI18n()

  const table = useServerTable({
    ...partnerList,
    defaultSort: { field: 'tier', order: 1 },
    columns: {
      name: { type: 'text' },
      tier: { type: 'number' },
      website: { type: 'text' },
      fromDate: { type: 'dateRange', fromParam: 'fromDateFrom', toParam: 'fromDateTo' },
    },
  })
  const dialog = useCrudDialog<Partner>()
  const actions = useCrudActions({
    dialog,
    create: useMutation(partnerMutations.create()),
    update: useMutation(partnerMutations.update()),
    remove: useMutation(partnerMutations.remove()),
    messages: {
      created: t('pages.admin.partners.toasts.created'),
      updated: t('pages.admin.partners.toasts.updated'),
      deleted: t('pages.admin.partners.toasts.deleted'),
      deleteHeader: t('pages.admin.partners.delete.header'),
      deleteMessage: (partner: Partner) =>
        t('pages.admin.partners.delete.message', { name: partner.name }),
    },
  })

  return { table, dialog, ...actions }
}
