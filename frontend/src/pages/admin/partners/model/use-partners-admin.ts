import { computed } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { partnerList, partnerMutations, type Partner, type PartnerInput } from '@/entities/partner'
import { useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Admin partner list: a table sorted by tier with a filter per column, the create/edit dialog and
 * deletion after confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function usePartnersAdmin() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

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
  const create = useMutation(partnerMutations.create())
  const update = useMutation(partnerMutations.update())
  const remove = useMutation(partnerMutations.remove())
  const saving = computed(() => create.isPending.value || update.isPending.value)

  function save(input: PartnerInput): void {
    const partner = dialog.editing.value
    if (partner) {
      update.mutate(
        { id: partner.id, input },
        feedback.outcome(t('pages.admin.partners.toasts.updated'), dialog.close),
      )
      return
    }
    create.mutate(input, feedback.outcome(t('pages.admin.partners.toasts.created'), dialog.close))
  }

  function confirmRemove(partner: Partner): void {
    confirmDelete({
      header: t('pages.admin.partners.delete.header'),
      message: t('pages.admin.partners.delete.message', { name: partner.name }),
      accept: () => {
        remove.mutate(partner.id, feedback.outcome(t('pages.admin.partners.toasts.deleted')))
      },
    })
  }

  return { table, dialog, saving, save, confirmRemove }
}
