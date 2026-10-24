import type { LegalPolicyResponse, LegalPolicyVersionResponse } from '@/shared/api/generated/models'

import { richText } from './common'

/** Version in force of a legal policy as `GET /api/legal-policies/:kind` returns it. */
export function buildLegalPolicy(
  overrides: Partial<LegalPolicyResponse> = {},
): LegalPolicyResponse {
  return {
    kind: 'Privacy',
    version: 2,
    content: richText('Policy body'),
    publishedAt: '2026-10-20T08:30:00Z',
    ...overrides,
  }
}

/** Publication of a version as `GET /api/legal-policies/:kind/versions` lists it. */
export function buildLegalPolicyVersion(
  overrides: Partial<LegalPolicyVersionResponse> = {},
): LegalPolicyVersionResponse {
  return { version: 1, publishedAt: '2026-09-01T08:30:00Z', ...overrides }
}
