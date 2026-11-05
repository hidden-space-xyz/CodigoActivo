/** Who the blocked data is made available to. */
export type BlockedDataAuthority =
  'Court' | 'Prosecutor' | 'DataProtectionAuthority' | 'PublicAdministration' | 'Claim'

/** What the signed-in administrator may do with the blocked data. */
export interface BlockedDataAccess {
  readonly isPrivacyOfficer: boolean
  readonly entities: readonly string[]
}

/** A consultation of the blocked data, without any of its data. */
export interface Consultation {
  readonly id: string
  readonly officerName: string | null
  readonly authority: BlockedDataAuthority
  readonly reference: string
  readonly openedAt: string
  readonly expiresAt: string
  readonly foundAccounts: number
  readonly extracts: number
}

/** Paging and sorting of the lists of consultations and holds. */
export interface BlockedDataListParams {
  page?: number
  pageSize?: number
  sort?: string
}

/**
 * A hold that keeps the history of an account and its minors past its retention while a claim is
 * open, without the accounts; `liftedAt` is `null` while it is active, and a name is `null` once
 * that officer's account no longer exists.
 */
export interface Hold {
  readonly id: string
  readonly authority: BlockedDataAuthority
  readonly reference: string
  readonly accounts: number
  readonly placedAt: string
  readonly placedByName: string | null
  readonly liftedAt: string | null
  readonly liftedByName: string | null
}

/** Number of history entries about an account for one kind of data. */
interface EntryCount {
  readonly entity: string
  readonly count: number
  readonly firstAt: string
  readonly lastAt: string
}

/** Account a search found, with only what tells it apart. */
export interface Candidate {
  readonly accountId: string
  readonly name: string
  readonly guardianId: string | null
  readonly exists: boolean
  readonly erasedAt: string | null
  readonly entries: readonly EntryCount[]
  readonly entryCount: number
}

/** The request a consultation is opened for, with the password that authorizes it. */
export interface ConsultationRequest {
  readonly authority: BlockedDataAuthority
  readonly reference: string
  readonly currentPassword: string
}

/** What opening a consultation sends. */
export interface OpenConsultationInput extends ConsultationRequest {
  readonly code: string
}

/** What taking an extract sends. */
export interface ExtractInput {
  readonly accountId: string
  readonly from: string | null
  readonly to: string | null
  readonly entities: readonly string[]
}

/** One history entry of the downloaded extract, with Spanish keys. */
interface ExtractFileEntry {
  readonly id: string
  readonly tipo: string
  readonly fecha: string
  readonly legible: boolean
  readonly operacion: string | null
  readonly realizadoPor: string | null
  readonly operacionDeLaAplicacion: string | null
  readonly valoresAnteriores: Readonly<Record<string, unknown>>
}

/** The downloaded extract, with Spanish keys. */
export interface ExtractFile {
  readonly generadoEl: string
  readonly destinatario: string
  readonly referencia: string
  readonly cuenta: string
  readonly desde: string | null
  readonly hasta: string | null
  readonly tiposDeDatos: readonly string[]
  readonly entradas: readonly ExtractFileEntry[]
  readonly datosActuales: unknown
}
