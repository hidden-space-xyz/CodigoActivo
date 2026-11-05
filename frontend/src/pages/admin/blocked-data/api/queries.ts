import { mutationOptions, queryOptions } from '@tanstack/vue-query'

import type { Translate } from '@/shared/i18n'
import type { ServerTableSource } from '@/shared/lib/paging'

import type {
  BlockedDataListParams,
  Consultation,
  ExtractInput,
  Hold,
  OpenConsultationInput,
} from '../model/types'
import {
  exportBlockedDataRequest,
  getBlockedDataAccessRequest,
  getConsultationPageRequest,
  getHoldPageRequest,
  liftHoldRequest,
  openConsultationRequest,
  placeHoldRequest,
  requestConsultationCodeRequest,
  searchBlockedDataRequest,
} from './requests'

/** Query keys of the blocked data. */
const blockedDataKeys = {
  all: ['blocked-data'] as const,
  access: () => [...blockedDataKeys.all, 'access'] as const,
  consultations: () => [...blockedDataKeys.all, 'consultations'] as const,
  holds: () => [...blockedDataKeys.all, 'holds'] as const,
}

/** Consultations, paged and sorted by the API. */
export const consultationList: ServerTableSource<Consultation, BlockedDataListParams> = {
  queryKey: blockedDataKeys.consultations(),
  fetchPage: (params) => getConsultationPageRequest(params),
}

/** Holds, paged and sorted by the API. */
export const holdList: ServerTableSource<Hold, BlockedDataListParams> = {
  queryKey: blockedDataKeys.holds(),
  fetchPage: (params) => getHoldPageRequest(params),
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
 * what the list of consultations shows, so all of them refresh it; placing and lifting a hold
 * refresh the list of holds; asking for the emailed code changes nothing shown. They are never
 * retried: each one is recorded.
 */
export const blockedDataMutations = {
  requestCode: () =>
    mutationOptions({
      mutationFn: (currentPassword: string) => requestConsultationCodeRequest(currentPassword),
    }),
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
  placeHold: () =>
    mutationOptions({
      mutationFn: ({ consultationId, accountId }: { consultationId: string; accountId: string }) =>
        placeHoldRequest(consultationId, accountId),
      meta: { invalidates: [blockedDataKeys.holds()] },
    }),
  liftHold: () =>
    mutationOptions({
      mutationFn: ({ consultationId, holdId }: { consultationId: string; holdId: string }) =>
        liftHoldRequest(consultationId, holdId),
      meta: { invalidates: [blockedDataKeys.holds()] },
    }),
}
