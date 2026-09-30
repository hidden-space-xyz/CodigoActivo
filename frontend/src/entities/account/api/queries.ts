import { queryOptions } from '@tanstack/vue-query'

import {
  getAccountCertificatesRequest,
  getAccountChildrenRequest,
  getAccountDeletionAllowedRequest,
  getAccountHistoryRequest,
  getAccountProfileRequest,
} from './requests'

/** Query keys of the signed-in account; `all` covers every one of them. */
export const accountKeys = {
  all: ['account'] as const,
  profile: () => [...accountKeys.all, 'profile'] as const,
  children: () => [...accountKeys.all, 'children'] as const,
  history: () => [...accountKeys.all, 'history'] as const,
  certificates: () => [...accountKeys.all, 'certificates'] as const,
  deletionAllowed: () => [...accountKeys.all, 'deletion-allowed'] as const,
}

/** Query options of the signed-in account. */
export const accountQueries = {
  /** The user's own profile, or `null` without a valid session. */
  profile: () =>
    queryOptions({
      queryKey: accountKeys.profile(),
      queryFn: () => getAccountProfileRequest(),
    }),
  /** The minors under the guardianship of `parentId`. */
  children: (parentId: string) =>
    queryOptions({
      queryKey: [...accountKeys.children(), parentId] as const,
      queryFn: () => getAccountChildrenRequest(parentId),
    }),
  /** The events the household took part in, upcoming and past. */
  history: () =>
    queryOptions({
      queryKey: accountKeys.history(),
      queryFn: () => getAccountHistoryRequest(),
    }),
  /** The participation certificates of the household. */
  certificates: () =>
    queryOptions({
      queryKey: accountKeys.certificates(),
      queryFn: () => getAccountCertificatesRequest(),
    }),
  /** Whether the user may delete the own account; only the initial administrator may not. */
  deletionAllowed: () =>
    queryOptions({
      queryKey: accountKeys.deletionAllowed(),
      queryFn: () => getAccountDeletionAllowedRequest(),
    }),
}
