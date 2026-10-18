import type { TranslationKey } from '@/shared/i18n'

type ProcessingId =
  | 'account'
  | 'nationalId'
  | 'minors'
  | 'signups'
  | 'terms'
  | 'statistics'
  | 'emails'
  | 'ratings'
  | 'security'
  | 'images'
  | 'socialMedia'
  | 'inquiries'
  | 'erasure'

/** One processing activity: the data it uses, why, on which legal basis and for how long. */
export interface ProcessingActivity {
  readonly id: ProcessingId
  readonly titleKey: TranslationKey
  readonly dataKey: TranslationKey
  readonly purposeKey: TranslationKey
  readonly legalBasisKey: TranslationKey
  readonly retentionKey: TranslationKey
}

function activity(id: ProcessingId): ProcessingActivity {
  const prefix = `pages.privacyPolicy.processing.items.${id}` as const
  return {
    id,
    titleKey: `${prefix}.title`,
    dataKey: `${prefix}.data`,
    purposeKey: `${prefix}.purpose`,
    legalBasisKey: `${prefix}.legalBasis`,
    retentionKey: `${prefix}.retention`,
  }
}

/** Every processing activity of personal data the site carries out, in the order the policy lists them. */
export const PROCESSING_ACTIVITIES: readonly ProcessingActivity[] = [
  activity('account'),
  activity('nationalId'),
  activity('minors'),
  activity('signups'),
  activity('terms'),
  activity('statistics'),
  activity('emails'),
  activity('ratings'),
  activity('security'),
  activity('images'),
  activity('socialMedia'),
  activity('inquiries'),
  activity('erasure'),
]

/** Website of the Spanish data protection authority, where complaints are lodged. */
export const AUTHORITY_URL = 'https://www.aepd.es'
