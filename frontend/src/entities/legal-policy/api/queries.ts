import { queryOptions } from '@tanstack/vue-query'

import type { LegalPolicyKind } from '../model/types'
import {
  getLegalPolicyRequest,
  getLegalPolicyVersionRequest,
  getLegalPolicyVersionsRequest,
} from './requests'

/** Query keys of the legal policies; `all` covers every policy and its versions. */
export const legalPolicyKeys = {
  all: ['legal-policies'] as const,
  current: (kind: LegalPolicyKind) => [...legalPolicyKeys.all, kind, 'current'] as const,
  versions: (kind: LegalPolicyKind) => [...legalPolicyKeys.all, kind, 'versions'] as const,
  version: (kind: LegalPolicyKind, version: number) =>
    [...legalPolicyKeys.versions(kind), version] as const,
}

/** Query options of the legal policies. */
export const legalPolicyQueries = {
  /** The version in force of a policy, `null` while none was published. */
  current: (kind: LegalPolicyKind) =>
    queryOptions({
      queryKey: legalPolicyKeys.current(kind),
      queryFn: () => getLegalPolicyRequest(kind),
    }),
  /** Every version of a policy, newest first; administrators only. */
  versions: (kind: LegalPolicyKind) =>
    queryOptions({
      queryKey: legalPolicyKeys.versions(kind),
      queryFn: () => getLegalPolicyVersionsRequest(kind),
    }),
  /** One version of a policy with its content; administrators only. */
  version: (kind: LegalPolicyKind, version: number) =>
    queryOptions({
      queryKey: legalPolicyKeys.version(kind, version),
      queryFn: () => getLegalPolicyVersionRequest(kind, version),
    }),
}
