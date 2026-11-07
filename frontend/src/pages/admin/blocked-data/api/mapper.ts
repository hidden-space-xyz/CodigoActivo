import type {
  BlockedDataAccessResponse,
  BlockedDataCandidateResponse,
  BlockedDataConsultationResponse,
  BlockedDataExport,
  BlockedDataHoldResponse,
} from '@/shared/api/generated/models'
import type { Translate } from '@/shared/i18n'

import { entityLabelKey } from '../lib/labels'
import type { BlockedDataAccess, Candidate, Consultation, ExtractFile, Hold } from '../model/types'

/** Maps what the signed-in administrator may do with the blocked data. */
export function toBlockedDataAccess(response: BlockedDataAccessResponse): BlockedDataAccess {
  return {
    isPrivacyOfficer: response.isPrivacyOfficer,
    entities: response.entities,
  }
}

/** Maps a consultation; `officerName` is `null` once the officer's account no longer exists. */
export function toConsultation(response: BlockedDataConsultationResponse): Consultation {
  return {
    id: response.id,
    officerName: response.officerName ?? null,
    requester: response.requester,
    reference: response.reference,
    openedAt: response.openedAt,
    expiresAt: response.expiresAt,
    foundAccounts: response.foundAccounts,
    extracts: response.extracts,
  }
}

/** Maps a hold; a name is `null` once that officer's account no longer exists. */
export function toHold(response: BlockedDataHoldResponse): Hold {
  return {
    id: response.id,
    requester: response.requester,
    reference: response.reference,
    accounts: response.accounts,
    placedAt: response.placedAt,
    placedByName: response.placedByName ?? null,
    liftedAt: response.liftedAt ?? null,
    liftedByName: response.liftedByName ?? null,
  }
}

/** Maps an account a search found, joining its names and adding up its history entries. */
export function toCandidate(response: BlockedDataCandidateResponse): Candidate {
  return {
    accountId: response.accountId,
    name: `${response.firstName} ${response.lastName}`.trim(),
    guardianId: response.guardianId ?? null,
    exists: response.exists,
    erasedAt: response.erasedAt ?? null,
    entries: response.entries,
    entryCount: response.entries.reduce((total, entry) => total + entry.count, 0),
  }
}

/** Builds the downloadable extract with Spanish keys and labels, ready to hand over. */
export function toExtractFile(data: BlockedDataExport, t: Translate): ExtractFile {
  return {
    generadoEl: data.generatedAt,
    destinatario: data.requester,
    referencia: data.reference,
    cuenta: data.accountId,
    desde: data.from ?? null,
    hasta: data.to ?? null,
    tiposDeDatos: data.entities.map((entity) => t(entityLabelKey(entity))),
    entradas: data.entries.map((entry) => ({
      id: entry.id,
      tipo: t(entityLabelKey(entry.entity)),
      fecha: entry.occurredAt,
      legible: entry.readable,
      operacion: entry.operation ?? null,
      realizadoPor: entry.actorId ?? null,
      operacionDeLaAplicacion: entry.command ?? null,
      valoresAnteriores: entry.values,
    })),
    datosActuales: data.currentData ?? null,
  }
}
