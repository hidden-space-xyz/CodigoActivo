import {
  getApiBlockedDataAccess,
  getApiBlockedDataConsultations,
  getApiBlockedDataHolds,
  postApiBlockedDataConsultations,
  postApiBlockedDataConsultationsCode,
  postApiBlockedDataConsultationsConsultationIdExtracts,
  postApiBlockedDataConsultationsConsultationIdHolds,
  postApiBlockedDataConsultationsConsultationIdHoldsHoldIdLift,
  postApiBlockedDataConsultationsConsultationIdSearches,
} from '@/shared/api/generated/endpoints/blocked-data/blocked-data'
import { toPage } from '@/shared/api'
import type { Translate } from '@/shared/i18n'
import type { ServerTablePage } from '@/shared/lib/paging'

import type {
  BlockedDataAccess,
  BlockedDataListParams,
  Candidate,
  Consultation,
  ExtractInput,
  Hold,
  OpenConsultationInput,
} from '../model/types'
import { toBlockedDataAccess, toCandidate, toConsultation, toExtractFile, toHold } from './mapper'

/** Tells whether the signed-in administrator may consult the blocked data. */
export async function getBlockedDataAccessRequest(): Promise<BlockedDataAccess> {
  const { data } = await getApiBlockedDataAccess()
  return toBlockedDataAccess(data)
}

/** Fetches one page of the consultations of the blocked data. */
export async function getConsultationPageRequest(
  params: BlockedDataListParams,
): Promise<ServerTablePage<Consultation>> {
  return toPage(await getApiBlockedDataConsultations(params), toConsultation)
}

/** Fetches one page of the holds on the blocked data. */
export async function getHoldPageRequest(
  params: BlockedDataListParams,
): Promise<ServerTablePage<Hold>> {
  return toPage(await getApiBlockedDataHolds(params), toHold)
}

/**
 * Asks the API to email the code that confirms opening a consultation
 * (`POST /api/blocked-data/consultations/code`). The password is verified first; an account with
 * an authenticator application skips this call and reads the code from its app instead.
 */
export async function requestConsultationCodeRequest(currentPassword: string): Promise<void> {
  await postApiBlockedDataConsultationsCode({ currentPassword })
}

/** Opens a consultation for a request. */
export async function openConsultationRequest(input: OpenConsultationInput): Promise<Consultation> {
  const { data } = await postApiBlockedDataConsultations(input)
  return toConsultation(data)
}

/** Looks accounts up in an open consultation. */
export async function searchBlockedDataRequest(
  consultationId: string,
  query: string,
): Promise<Candidate[]> {
  const { data } = await postApiBlockedDataConsultationsConsultationIdSearches(consultationId, {
    query,
  })
  return data.candidates.map(toCandidate)
}

/** Takes an extract about an account and returns it as a JSON file with Spanish keys. */
export async function exportBlockedDataRequest(
  consultationId: string,
  input: ExtractInput,
  t: Translate,
): Promise<Blob> {
  const { data } = await postApiBlockedDataConsultationsConsultationIdExtracts(consultationId, {
    accountId: input.accountId,
    from: input.from,
    to: input.to,
    entities: [...input.entities],
  })
  return new Blob([JSON.stringify(toExtractFile(data, t), null, 2)], {
    type: 'application/json',
  })
}

/**
 * Keeps the history of an account an open consultation found, and of its minors, past its
 * retention while the claim of the consultation is open.
 */
export async function placeHoldRequest(consultationId: string, accountId: string): Promise<Hold> {
  const { data } = await postApiBlockedDataConsultationsConsultationIdHolds(consultationId, {
    accountId,
  })
  return toHold(data)
}

/** Lifts a hold from an open consultation once its claim is closed. */
export async function liftHoldRequest(consultationId: string, holdId: string): Promise<void> {
  await postApiBlockedDataConsultationsConsultationIdHoldsHoldIdLift(consultationId, holdId)
}
