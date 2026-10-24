import { mutationOptions } from '@tanstack/vue-query'

import type { LegalPolicyKind } from '../model/types'
import { legalPolicyKeys } from './queries'
import { publishLegalPolicyRequest } from './requests'

/** Mutation options of the legal policies; each one refreshes every policy query on success. */
export const legalPolicyMutations = {
  publish: () =>
    mutationOptions({
      mutationFn: ({ kind, content }: { kind: LegalPolicyKind; content: string }) =>
        publishLegalPolicyRequest(kind, content),
      meta: { invalidates: [legalPolicyKeys.all] },
    }),
}
