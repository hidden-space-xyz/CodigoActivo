import type { Partner, PartnerInput } from '@/entities/partner'
import { parseDateOnly, toDateOnly } from '@/shared/lib/date'
import type { FormProblem, FormReading } from '@/shared/lib/form'

/** What the partner dialog binds its inputs to; the day comes from a date picker. */
export interface PartnerDraft {
  name: string
  fromDate: Date | null
  tier: number | undefined
  website: string
}

/** Field of the partner form that can be refused. */
type PartnerField = 'name' | 'fromDate'

/** A blank draft, or one filled with the partner being edited. */
export function toPartnerDraft(partner: Partner | null): PartnerDraft {
  return {
    name: partner?.name ?? '',
    fromDate: parseDateOnly(partner?.fromDate),
    tier: partner?.tier ?? 0,
    website: partner?.website ?? '',
  }
}

/**
 * Reads the partner dialog: a name and a start day are required, an empty tier counts as 0 and a
 * blank website is saved as none. The thumbnail is resolved separately when saving.
 */
export function readPartnerDraft(
  draft: PartnerDraft,
): FormReading<PartnerField, Omit<PartnerInput, 'thumbnailId'>> {
  const name = draft.name.trim()
  const problems: Partial<Record<PartnerField, FormProblem>> = {}
  if (!name) problems.name = 'pages.admin.partners.form.problems.nameRequired'
  if (!draft.fromDate) problems.fromDate = 'pages.admin.partners.form.problems.fromDateRequired'
  if (!name || !draft.fromDate) return { problems, value: null }

  const website = draft.website.trim()
  return {
    problems,
    value: {
      name,
      fromDate: toDateOnly(draft.fromDate),
      tier: draft.tier ?? 0,
      website: website || null,
    },
  }
}
