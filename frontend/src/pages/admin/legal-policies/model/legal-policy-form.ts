import type { LegalPolicy } from '@/entities/legal-policy'
import type { FormProblem, FormReading } from '@/shared/lib/form'
import {
  isRichTextBlank,
  isRichTextTooLong,
  parseRichText,
  serializeRichText,
} from '@/shared/lib/rich-text'

/** What the policy editor binds to; `content` is a rich-text document. */
export interface LegalPolicyDraft {
  content: string
}

/** Field of the policy editor that can be refused. */
type LegalPolicyField = 'content'

/** A blank draft, or one filled with the version in force. */
export function toLegalPolicyDraft(policy: LegalPolicy | null | undefined): LegalPolicyDraft {
  return { content: policy?.content ?? '' }
}

/**
 * Whether two rich-text documents hold the same text-only content, however their JSON is laid out:
 * the API stores it as `jsonb`, which reorders keys, while the editor sends it compact.
 */
export function isSameLegalPolicyContent(a: string, b: string): boolean {
  return serializeRichText(parseRichText(a)) === serializeRichText(parseRichText(b))
}

/** Reads the policy editor: some text is required, and it must fit what the API stores. */
export function readLegalPolicyDraft(
  draft: LegalPolicyDraft,
): FormReading<LegalPolicyField, LegalPolicyDraft> {
  const problems: Partial<Record<LegalPolicyField, FormProblem>> = {}
  if (isRichTextBlank(draft.content)) {
    problems.content = 'pages.admin.legalPolicies.form.problems.contentRequired'
  } else if (isRichTextTooLong(draft.content)) {
    problems.content = 'validation.richTextTooLong'
  }
  if (problems.content) return { problems, value: null }
  return { problems, value: { content: draft.content } }
}
