import type { Gender } from '@/entities/user/@x/account'

/** Signed-in user's own data shown and edited on the account page. */
export interface AccountProfile {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  /** Optional second contact phone; `''` when none is set. */
  readonly secondaryPhone: string
  /** Normalized DNI or NIE identifying the adult. */
  readonly nationalId: string
  /** Whether the user agreed to receive news by email. */
  readonly newsConsent: boolean
  readonly gender: Gender
  readonly statusName: string
  readonly isAdmin: boolean
}

/**
 * Enrollment data of an authenticator application that still has to be confirmed with its first
 * code. `sharedKey` is grouped for manual entry; `authenticatorUri` is what the QR code encodes.
 */
export interface AuthenticatorSetup {
  readonly sharedKey: string
  readonly authenticatorUri: string
}

/** Minor managed by the signed-in adult. */
export interface AccountChild {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly birthDate: string
  readonly gender: Gender
}

/** One household member's participation in an activity of a past or upcoming event. */
export interface AccountHistoryActivity {
  readonly activityId: string
  readonly title: string
  readonly location: string
  readonly modality: string
  readonly participantId: string
  readonly participantName: string
  /** `true` when the participant is the signed-in user rather than one of their minors. */
  readonly isSelf: boolean
  readonly roleName: string
  readonly statusName: string
}

/** Participation certificate for the user or a minor, rendered client-side as a printable sheet. */
export interface AccountCertificate {
  /** Reference code printed on the certificate. */
  readonly code: string
  readonly eventId: string
  readonly participantId: string
  readonly firstName: string
  readonly lastName: string
  readonly eventTitle: string
  readonly startsAt: string
  readonly endsAt: string
}

/**
 * Consent decided for the user or a minor about a terms document of an event. `withdrawn` tells a
 * consent given and withdrawn later from one never given.
 */
export interface AccountConsent {
  readonly eventId: string
  readonly eventTitle: string
  readonly eventStartsAt: string
  readonly termsDocumentId: string
  readonly documentName: string
  readonly personId: string
  readonly personName: string
  readonly accepted: boolean
  readonly withdrawn: boolean
  readonly decidedAt: string
  /** Full name of who last decided: the person or their guardian. */
  readonly decidedByName: string
}

/** Event in the account history, grouping the activities the household joined. */
export interface AccountHistoryEntry {
  readonly eventId: string
  readonly title: string
  readonly subtitle: string
  readonly startsAt: string
  readonly endsAt: string
  readonly thumbnailId: string
  readonly isPast: boolean
  readonly canRate: boolean
  readonly activities: readonly AccountHistoryActivity[]
}

/**
 * Copy of the personal data kept about the signed-in user and the minors in their care, as the
 * file they download: Spanish names and labels, so they can read it as well as take it elsewhere.
 * Dates are ISO 8601 instants and the signatures base64 PNG images.
 */
export interface PersonalDataFile {
  readonly generadoEl: string
  readonly cuenta: {
    readonly id: string
    readonly nombre: string
    readonly apellidos: string
    readonly email: string | null
    readonly emailNuevoSinConfirmar: string | null
    readonly telefono: string | null
    readonly segundoTelefono: string | null
    readonly dniNie: string | null
    readonly genero: string
    readonly aceptaNovedades: boolean
    readonly relacionConLaAsociacion: string
    readonly estado: string
    readonly administraLaWeb: boolean
    readonly verificacionEnDosPasos: string
    readonly altaEl: string
    readonly modificadaEl: string | null
    readonly ultimoAccesoEl: string | null
    readonly declaraSerMayorDeEdadEl: string | null
  }
  readonly cambiosDeLaPreferenciaSobreNovedades: readonly {
    readonly acepta: boolean
    readonly medio: string
    readonly fecha: string
  }[]
  readonly menores: readonly {
    readonly id: string
    readonly nombre: string
    readonly apellidos: string
    readonly fechaDeNacimiento: string | null
    readonly genero: string
    readonly relacionConLaAsociacion: string
    readonly altaEl: string
    readonly modificadoEl: string | null
    readonly declarasSerSuPadreMadreOTutorEl: string | null
    readonly teAvisamosDeQueCumple18El: string | null
  }[]
  readonly inscripciones: readonly {
    readonly personaId: string
    readonly persona: string
    readonly evento: string
    readonly actividad: string
    readonly inicioDeLaActividad: string
    readonly rol: string
    readonly estado: string
    readonly inscritaEl: string
  }[]
  readonly decisionesSobreDocumentos: readonly {
    readonly personaId: string
    readonly persona: string
    readonly decidioPor: string
    readonly evento: string
    readonly documento: string
    readonly versionDelDocumento: number
    readonly aceptado: boolean
    readonly fecha: string
    readonly firmaPngBase64: string | null
    readonly cambios: readonly {
      readonly aceptado: boolean
      readonly versionDelDocumento: number
      readonly fecha: string
      readonly decidioPor: string
    }[]
  }[]
  readonly sesionesAbiertas: readonly {
    readonly abiertaEl: string
    readonly caducaEl: string
  }[]
  readonly registroDeSeguridad: readonly {
    readonly fecha: string
    readonly operacion: string
    readonly resultado: string
    readonly quien: string
    readonly ip: string | null
  }[]
}
