import { mutationOptions, queryOptions } from '@tanstack/vue-query'

import type { Translate } from '@/shared/i18n'
import type { ServerTableSource } from '@/shared/lib/paging'

import type {
  Consultation,
  ConsultationListParams,
  ExtractInput,
  OpenConsultationInput,
} from '../model/types'
import {
  exportBlockedDataRequest,
  getBlockedDataAccessRequest,
  getConsultationPageRequest,
  openConsultationRequest,
  searchBlockedDataRequest,
} from './requests'

/** Query keys of the blocked data. */
const blockedDataKeys = {
  all: ['blocked-data'] as const,
  access: () => [...blockedDataKeys.all, 'access'] as const,
  consultations: () => [...blockedDataKeys.all, 'consultations'] as const,
}

/** Consultations, paged and sorted by the API. */
export const consultationList: ServerTableSource<Consultation, ConsultationListParams> = {
  queryKey: blockedDataKeys.consultations(),
  fetchPage: (params) => getConsultationPageRequest(params),
}

/** Query options of the blocked data. */
export const blockedDataQueries = {
  /** Whether the signed-in administrator may consult the blocked data. */
  access: () =>
    queryOptions({
      queryKey: blockedDataKeys.access(),
      queryFn: () => getBlockedDataAccessRequest(),
    }),
}

/**
 * Mutation options of the consultations. Opening one, searching and taking an extract change
 * what the list shows, so all of them refresh it. They are never retried: each one is recorded.
 */
export const blockedDataMutations = {
  open: () =>
    mutationOptions({
      mutationFn: (input: OpenConsultationInput) => openConsultationRequest(input),
      meta: { invalidates: [blockedDataKeys.consultations()] },
    }),
  search: () =>
    mutationOptions({
      mutationFn: ({ consultationId, query }: { consultationId: string; query: string }) =>
        searchBlockedDataRequest(consultationId, query),
      meta: { invalidates: [blockedDataKeys.consultations()] },
    }),
  extract: (t: Translate) =>
    mutationOptions({
      mutationFn: ({ consultationId, input }: { consultationId: string; input: ExtractInput }) =>
        exportBlockedDataRequest(consultationId, input, t),
      meta: { invalidates: [blockedDataKeys.consultations()] },
    }),
}
