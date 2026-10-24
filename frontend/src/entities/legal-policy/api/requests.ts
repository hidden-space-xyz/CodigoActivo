import {
  getApiLegalPoliciesKind,
  getApiLegalPoliciesKindVersions,
  getApiLegalPoliciesKindVersionsVersion,
  putApiLegalPoliciesKind,
} from '@/shared/api/generated/endpoints/legal/legal'
import { unwrapOrNull } from '@/shared/api'

import type { LegalPolicy, LegalPolicyKind, LegalPolicyVersion } from '../model/types'
import { toLegalPolicy, toLegalPolicyVersion } from './mapper'

/** Loads the version in force of a policy; `null` while none was published. */
export async function getLegalPolicyRequest(kind: LegalPolicyKind): Promise<LegalPolicy | null> {
  const policy = await unwrapOrNull(getApiLegalPoliciesKind(kind))
  return policy ? toLegalPolicy(policy) : null
}

/** Lists every version of a policy, newest first; empty while none was published. */
export async function getLegalPolicyVersionsRequest(
  kind: LegalPolicyKind,
): Promise<readonly LegalPolicyVersion[]> {
  const versions = await unwrapOrNull(getApiLegalPoliciesKindVersions(kind))
  return (versions ?? []).map(toLegalPolicyVersion)
}

/** Loads one version of a policy with its content. */
export async function getLegalPolicyVersionRequest(
  kind: LegalPolicyKind,
  version: number,
): Promise<LegalPolicy> {
  const response = await getApiLegalPoliciesKindVersionsVersion(kind, version)
  return toLegalPolicy(response.data)
}

/** Publishes a new version of a policy; the same content as the one in force changes nothing. */
export async function publishLegalPolicyRequest(
  kind: LegalPolicyKind,
  content: string,
): Promise<void> {
  await putApiLegalPoliciesKind(kind, { content })
}
