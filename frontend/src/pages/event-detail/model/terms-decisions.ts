import type { TermsDecisionInput } from '@/entities/activity'
import type { EventTermsDocumentState, EventTermsPerson } from '@/entities/event'

/**
 * Who decides a document in the terms dialog: the signed-in user, for themselves and, for a minor
 * in their care, the conditions and the consents before the age of 14; or the minor, who decides
 * their own consents on the signed-in user's device from the age of 14.
 */
export type TermsDecider = 'user' | 'person'

/** A document still to decide for a person, and who decides it. */
export interface TermsDialogItem {
  readonly document: EventTermsDocumentState
  readonly decider: TermsDecider
}

/** A person being signed up and the documents still to decide for them. */
export interface TermsDialogSection {
  readonly personId: string
  /** First name of the person; the dialog names the signed-in user differently. */
  readonly name: string
  /** The person is the signed-in user. */
  readonly self: boolean
  readonly items: readonly TermsDialogItem[]
}

/** What the terms dialog collected, by `draftKey`: the checked boxes and the signatures drawn. */
export interface TermsDialogDraft {
  readonly checked: Readonly<Record<string, boolean>>
  /** PNG data URLs; an empty or missing entry means nothing is drawn. */
  readonly signatures: Readonly<Record<string, string>>
}

/**
 * What the terms dialog read: why it cannot be confirmed yet or, if nothing is missing, the
 * decisions.
 */
export interface TermsDialogReading {
  /** A required document is not checked for someone. */
  readonly missingRequired: boolean
  /** A checked document that asks for a signature has none drawn. */
  readonly missingSignature: boolean
  /** The decisions of each person, by person id, or `null` while something is missing. */
  readonly decisions: Readonly<Record<string, TermsDecisionInput[]>> | null
}

/** Key of one person's decision on one document in the dialog draft. */
export function draftKey(personId: string, documentId: string): string {
  return `${personId}__${documentId}`
}

/** Documents still to decide for a person: those not accepted for them yet. */
export function pendingDocuments(
  documents: readonly EventTermsDocumentState[],
  person: EventTermsPerson | undefined,
): EventTermsDocumentState[] {
  return documents.filter(
    (document) =>
      !person?.decisions.some(
        (decision) => decision.documentId === document.id && decision.accepted,
      ),
  )
}

/**
 * Builds a dialog section for each person being signed up who still has documents to decide, in the
 * order given. A person missing from `people` is treated as having decided nothing, with every
 * document decided by the signed-in user.
 *
 * @param documents - Documents of the event.
 * @param people - Household members with the decisions taken for them.
 * @param personIds - People being signed up.
 * @param selfId - The signed-in user.
 * @param names - First name of a person missing from `people`.
 */
export function termsSections(
  documents: readonly EventTermsDocumentState[],
  people: readonly EventTermsPerson[],
  personIds: readonly string[],
  selfId: string,
  names: (personId: string) => string,
): TermsDialogSection[] {
  return personIds.flatMap((personId) => {
    const person = people.find((candidate) => candidate.id === personId)
    const self = personId === selfId
    const items = pendingDocuments(documents, person).map((document) => ({
      document,
      decider:
        !self && document.kind === 'Consent' && person?.decidesOwnConsents
          ? ('person' as const)
          : ('user' as const),
    }))
    return items.length > 0
      ? [{ personId, name: person?.firstName ?? names(personId), self, items }]
      : []
  })
}

/** Whether `document` shows its signature pad for `personId`: it asks for one and is checked. */
export function asksForSignature(
  personId: string,
  document: EventTermsDocumentState,
  draft: TermsDialogDraft,
): boolean {
  return document.requiresSignature && !!draft.checked[draftKey(personId, document.id)]
}

/**
 * Reads the terms dialog: every required document must be checked for every person and every
 * checked document that asks for a signature needs one drawn. Decisions carry a signature only for
 * those documents.
 */
export function readTermsDecisions(
  sections: readonly TermsDialogSection[],
  draft: TermsDialogDraft,
): TermsDialogReading {
  const entries = sections.flatMap((section) =>
    section.items.map(({ document }) => ({ personId: section.personId, document })),
  )
  const missingRequired = entries.some(
    ({ personId, document }) =>
      document.required && !draft.checked[draftKey(personId, document.id)],
  )
  const missingSignature = entries.some(
    ({ personId, document }) =>
      asksForSignature(personId, document, draft) &&
      !draft.signatures[draftKey(personId, document.id)],
  )
  if (missingRequired || missingSignature) {
    return { missingRequired, missingSignature, decisions: null }
  }
  const decisions: Record<string, TermsDecisionInput[]> = {}
  for (const { personId, document } of entries) {
    const key = draftKey(personId, document.id)
    const signature = asksForSignature(personId, document, draft) ? draft.signatures[key] : ''
    decisions[personId] = [
      ...(decisions[personId] ?? []),
      {
        termsDocumentId: document.id,
        version: document.version,
        accepted: !!draft.checked[key],
        ...(signature ? { signature } : {}),
      },
    ]
  }
  return { missingRequired, missingSignature, decisions }
}
