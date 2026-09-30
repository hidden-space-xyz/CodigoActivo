import { mutationOptions } from '@tanstack/vue-query'

import type { TermsDocumentInput } from '../model/types'
import { termsDocumentKeys } from './queries'
import {
  createTermsDocumentRequest,
  deleteTermsDocumentRequest,
  updateTermsDocumentRequest,
} from './requests'

/** Mutation options of terms documents; each one refreshes every terms query on success. */
export const termsDocumentMutations = {
  create: () =>
    mutationOptions({
      mutationFn: (input: TermsDocumentInput) => createTermsDocumentRequest(input),
      meta: { invalidates: [termsDocumentKeys.all] },
    }),
  update: () =>
    mutationOptions({
      mutationFn: ({ id, input }: { id: string; input: TermsDocumentInput }) =>
        updateTermsDocumentRequest(id, input),
      meta: { invalidates: [termsDocumentKeys.all] },
    }),
  remove: () =>
    mutationOptions({
      mutationFn: (id: string) => deleteTermsDocumentRequest(id),
      meta: { invalidates: [termsDocumentKeys.all] },
    }),
}
