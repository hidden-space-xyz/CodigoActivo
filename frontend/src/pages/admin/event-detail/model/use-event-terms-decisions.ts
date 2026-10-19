import { reactive } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { eventMutations } from '@/entities/event'
import { fullName } from '@/entities/user'
import { alsoInvalidates } from '@/shared/api'
import { useActionConfirm, useCrudFeedback } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

import { eventReportKeys, termsDecisionList } from '../api/queries'
import type { EventTermsDecision } from './types'

/**
 * Terms tab of an event's admin page: the decisions taken about its terms documents, latest first,
 * with a person search and a document filter, plus the popup that shows a signature in full and
 * the withdrawal of a consent someone asked for by email. Rows are keyed by person and document,
 * since a decision has no id of its own. The list only loads while the tab is `active`. Call it in
 * `setup`.
 *
 * @param eventId - Getter so the list follows route changes.
 * @param active - Getter telling whether the tab is selected.
 */
export function useEventTermsDecisions(eventId: () => string, active: () => boolean) {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const { confirmAction } = useActionConfirm()

  const table = useServerTable({
    ...termsDecisionList,
    defaultSort: { field: 'decidedAt', order: -1 },
    rowKey: (decision) => `${decision.userId}:${decision.termsDocumentId}`,
    columns: {
      person: { type: 'text', param: 'search' },
      document: { param: 'termsDocumentId' },
    },
    extraParams: () => ({ eventId: eventId() }),
    enabled: active,
  })

  const withdrawal = useMutation(() =>
    alsoInvalidates(eventMutations.withdrawConsent(), eventReportKeys.termsDecisions()),
  )

  const signature = reactive<{ visible: boolean; decision: EventTermsDecision | null }>({
    visible: false,
    decision: null,
  })

  /** Opens the popup with the signature of `decision`. */
  function openSignature(decision: EventTermsDecision): void {
    signature.decision = decision
    signature.visible = true
  }

  /** Asks for confirmation and records the withdrawal of the consent `decision` gave. */
  function withdraw(decision: EventTermsDecision): void {
    confirmAction({
      header: t('pages.admin.eventDetail.terms.withdraw.header'),
      message: t('pages.admin.eventDetail.terms.withdraw.message', {
        document: decision.documentName,
        name: fullName(decision),
      }),
      acceptLabel: t('pages.admin.eventDetail.terms.withdraw.confirm'),
      accept: () => {
        withdrawal.mutate(
          {
            eventId: eventId(),
            userId: decision.userId,
            termsDocumentId: decision.termsDocumentId,
          },
          feedback.outcome(t('pages.admin.eventDetail.terms.withdraw.done')),
        )
      },
    })
  }

  return { table, signature, openSignature, withdraw }
}
