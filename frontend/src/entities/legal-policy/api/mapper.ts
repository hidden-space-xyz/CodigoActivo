import type { LegalPolicyResponse, LegalPolicyVersionResponse } from '@/shared/api/generated/models'

import type { LegalPolicy, LegalPolicyVersion } from '../model/types'

/** Maps a version of a legal policy with its content. */
export function toLegalPolicy(policy: LegalPolicyResponse): LegalPolicy {
  return {
    kind: policy.kind,
    version: policy.version,
    content: policy.content,
    publishedAt: policy.publishedAt,
  }
}

/** Maps the publication date of a version of a legal policy. */
export function toLegalPolicyVersion(version: LegalPolicyVersionResponse): LegalPolicyVersion {
  return { version: version.version, publishedAt: version.publishedAt }
}
