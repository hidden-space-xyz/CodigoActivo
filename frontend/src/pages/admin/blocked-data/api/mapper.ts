import type {
  BlockedDataAccessResponse,
  BlockedDataCandidateResponse,
  BlockedDataConsultationResponse,
  BlockedDataExport,
} from '@/shared/api/generated/models'
import type { Translate } from '@/shared/i18n'

import { authorityLabelKey, entityLabelKey } from '../lib/labels'
import type { BlockedDataAccess, Candidate, Consultation, ExtractFile } from '../model/types'

/** Maps what the signed-in administrator may do with the blocked data. */
export function toBlockedDataAccess(response: BlockedDataAccessResponse): BlockedDataAccess {
  return {
    isPrivacyOfficer: response.isPrivacyOfficer,
    usesAuthenticator: response.usesAuthenticator,
    entities: response.entities,
  }
}

/** Maps a consultation. */
export function toConsultation(response: BlockedDataConsultationResponse): Consultation {
  return {
    id: response.id,
    officerName: response.officerName ?? null,
    authority: response.authority,
    reference: response.reference,
    openedAt: response.openedAt,
    expiresAt: response.expiresAt,
    foundAccounts: response.foundAccounts,
    extracts: response.extracts,
  }
}

/** Maps an account a search found. */
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
    destinatario: t(authorityLabelKey(data.authority)),
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
