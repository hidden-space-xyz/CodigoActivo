import type { TermsDocument, TermsDocumentInput } from '@/entities/terms-document'
import type { FormProblem, FormReading } from '@/shared/lib/form'
import { isRichTextBlank } from '@/shared/lib/rich-text'

/** What the terms document dialog binds its inputs to; `description` is a rich-text document. */
export interface TermsDocumentDraft {
  name: string
  description: string
}

/** Field of the terms document form that can be refused. */
type TermsDocumentField = 'name' | 'description'

/** A blank draft, or one filled with the terms document being edited. */
export function toTermsDocumentDraft(termsDocument: TermsDocument | null): TermsDocumentDraft {
  return { name: termsDocument?.name ?? '', description: termsDocument?.description ?? '' }
}

/** Reads the terms document dialog: a name and some content are required. */
export function readTermsDocumentDraft(
  draft: TermsDocumentDraft,
): FormReading<TermsDocumentField, TermsDocumentInput> {
  const name = draft.name.trim()
  const problems: Partial<Record<TermsDocumentField, FormProblem>> = {}
  if (!name) problems.name = true
  if (isRichTextBlank(draft.description)) {
    problems.description = 'pages.admin.catalogs.termsDocuments.form.problems.contentRequired'
  }
  if (Object.keys(problems).length > 0) return { problems, value: null }
  return { problems, value: { name, description: draft.description } }
}
