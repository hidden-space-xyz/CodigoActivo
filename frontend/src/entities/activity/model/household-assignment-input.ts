import type { TermsDecisionInput } from './types'

/**
 * Role picked for one household member in a bulk household signup for an activity, with the
 * decisions just taken for that member on the event's terms documents.
 */
export interface HouseholdAssignmentInput {
  readonly userId: string
  readonly roleId: string
  readonly termsDecisions?: readonly TermsDecisionInput[] | undefined
}
