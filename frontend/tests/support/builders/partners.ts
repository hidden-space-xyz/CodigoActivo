import type { PartnerResponse } from '@/shared/api/generated/models'

import { AUDIT } from './common'

/** Partner as returned by `GET /api/partners`. */
export function buildPartnerResponse(overrides: Partial<PartnerResponse> = {}): PartnerResponse {
  return {
    id: 'partner-1',
    name: 'Acme',
    tier: 1,
    website: 'https://acme.test',
    fromDate: '2024-03-15',
    thumbnailId: 'thumb-partner-1',
    ...AUDIT,
    createdAt: '2024-03-15T10:00:00Z',
    updatedAt: null,
    updatedBy: null,
    ...overrides,
  }
}
