import { reactive } from 'vue'

import { useServerTable } from '@/shared/lib/paging'

import { termsDecisionList } from '../api/queries'
import type { EventTermsDecision } from './types'

/**
 * Terms tab of an event's admin page: the decisions taken about its terms documents, latest first,
 * with a person search and a document filter, plus the popup that shows a signature in full. Rows
 * are keyed by person and document, since a decision has no id of its own. The list only loads
 * while the tab is `active`. Call it in `setup`.
 *
 * @param eventId - Getter so the list follows route changes.
 * @param active - Getter telling whether the tab is selected.
 */
export function useEventTermsDecisions(eventId: () => string, active: () => boolean) {
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

  const signature = reactive<{ visible: boolean; decision: EventTermsDecision | null }>({
    visible: false,
    decision: null,
  })

  /** Opens the popup with the signature of `decision`. */
  function openSignature(decision: EventTermsDecision): void {
    signature.decision = decision
    signature.visible = true
  }

  return { table, signature, openSignature }
}
