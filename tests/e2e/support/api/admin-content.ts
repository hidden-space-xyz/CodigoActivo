import type { LegalPolicyKind } from '@contract/legalPolicyKind'
import type { LegalPolicyResponse } from '@contract/legalPolicyResponse'

import type { ApiClient } from './api-client'

/** The number of the version of the policy `kind` in force, as anyone can read it. */
export async function versionInForce(api: ApiClient, kind: LegalPolicyKind): Promise<number> {
  const policy = await api.get<LegalPolicyResponse>(`/api/legal-policies/${kind}`)
  return policy.version
}
