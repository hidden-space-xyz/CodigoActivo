import type { TermsDecisionInput } from '@/entities/activity'
import type { EventTermsDocumentState } from '@/entities/event'

/** What the terms dialog collected, by document id: the checked boxes and the signatures drawn. */
export interface TermsDialogDraft {
  readonly checked: Readonly<Record<string, boolean>>
  /** PNG data URLs; an empty or missing entry means nothing is drawn. */
  readonly signatures: Readonly<Record<string, string>>
}

/** What the terms dialog read: why it cannot be confirmed yet or, if nothing is missing, the decisions. */
export interface TermsDialogReading {
  /** A required document is not checked. */
  readonly missingRequired: boolean
  /** A checked document that asks for a signature has none drawn. */
  readonly missingSignature: boolean
  /** One decision per document, or `null` while something is missing. */
  readonly decisions: TermsDecisionInput[] | null
}

/** Whether `document` shows its signature pad: it asks for a signature and is checked. */
export function asksForSignature(
  document: EventTermsDocumentState,
  draft: TermsDialogDraft,
): boolean {
  return document.requiresSignature && !!draft.checked[document.id]
}

/**
 * Reads the terms dialog: every required document must be checked and every checked document that
 * asks for a signature needs one drawn. Decisions carry a signature only for those documents.
 */
export function readTermsDecisions(
  documents: readonly EventTermsDocumentState[],
  draft: TermsDialogDraft,
): TermsDialogReading {
  const missingRequired = documents.some(
    (document) => document.required && !draft.checked[document.id],
  )
  const missingSignature = documents.some(
    (document) => asksForSignature(document, draft) && !draft.signatures[document.id],
  )
  if (missingRequired || missingSignature) {
    return { missingRequired, missingSignature, decisions: null }
  }
  return {
    missingRequired,
    missingSignature,
    decisions: documents.map((document) => {
      const signature = asksForSignature(document, draft) ? draft.signatures[document.id] : ''
      return {
        termsDocumentId: document.id,
        accepted: !!draft.checked[document.id],
        ...(signature ? { signature } : {}),
      }
    }),
  }
}
