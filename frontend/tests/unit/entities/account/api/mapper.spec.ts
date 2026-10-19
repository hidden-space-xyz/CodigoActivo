import { describe, expect, it } from 'vitest'

import {
  toAccountCertificate,
  toAccountChild,
  toAccountHistoryEntry,
  toAccountProfile,
  toAuthenticatorSetup,
  toMinorRequest,
  toPersonalDataFile,
  toRegisterRequest,
  toSaveEventRatingRequest,
  toUpdateMinorRequest,
  toUpdateProfileRequest,
} from '@/entities/account/api/mapper'

import {
  buildCertificateResponse,
  buildDependentResponse,
  buildHistoryActivityResponse,
  buildHistoryResponse,
  buildPersonalDataResponse,
  buildUserResponse,
} from '../../../../support/builders'
import { t } from '../../../../support/render'

const MINOR = {
  firstName: 'Byron',
  lastName: 'Lovelace',
  birthDate: '2015-03-02',
  gender: 'Male',
} as const

describe('account mapper', () => {
  it('maps the own profile, missing contact details as empty text', () => {
    expect(toAccountProfile(buildUserResponse({ email: null, isAdmin: true }))).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: '',
      phone: '600000000',
      secondaryPhone: '',
      nationalId: '12345678Z',
      newsConsent: false,
      gender: 'Female',
      statusName: 'Active',
      isAdmin: true,
    })
  })

  it('maps the authenticator enrollment and the minors', () => {
    expect(
      toAuthenticatorSetup({ sharedKey: 'ABCD EFGH', authenticatorUri: 'otpauth://x' }),
    ).toEqual({
      sharedKey: 'ABCD EFGH',
      authenticatorUri: 'otpauth://x',
    })
    expect(toAccountChild(buildDependentResponse())).toEqual({
      id: 'child-1',
      firstName: 'Byron',
      lastName: 'Lovelace',
      birthDate: '2015-03-02',
      gender: 'Male',
    })
  })

  it('builds the profile update of an adult without birth date nor guardian', () => {
    expect(
      toUpdateProfileRequest({
        firstName: 'Ada',
        lastName: 'King',
        email: 'ada@example.test',
        phone: '600000000',
        secondaryPhone: null,
        nationalId: '12345678Z',
        newsConsent: true,
        gender: 'Female',
        currentPassword: null,
      }),
    ).toMatchObject({ lastName: 'King', birthDate: null, parentId: null, currentPassword: null })
  })

  it('builds the minor bodies, keeping an edited minor under its guardian', () => {
    expect(toMinorRequest(MINOR)).toEqual({ ...MINOR, declaresGuardianship: true })
    expect(toUpdateMinorRequest(MINOR, 'user-1')).toEqual({
      ...MINOR,
      newsConsent: false,
      parentId: 'user-1',
    })
  })

  it('joins participant names in the history and keeps rating flags', () => {
    const entry = toAccountHistoryEntry(
      buildHistoryResponse({
        isPast: true,
        canRate: true,
        activities: [buildHistoryActivityResponse({ firstName: 'Byron', isSelf: false })],
      }),
    )

    expect(entry).toMatchObject({ eventId: 'event-1', isPast: true, canRate: true })
    expect(entry.activities).toEqual([
      {
        activityId: 'activity-1',
        title: 'Taller de robótica',
        location: 'Aula 1',
        modality: 'Presencial',
        participantId: 'user-1',
        participantName: 'Byron Lovelace',
        isSelf: false,
        roleName: 'Participante',
        statusName: 'Confirmada',
      },
    ])
  })

  it('maps a certificate', () => {
    expect(toAccountCertificate(buildCertificateResponse())).toEqual({
      code: 'CA-2025-0001',
      eventId: 'event-1',
      participantId: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      isSelf: true,
      eventTitle: 'Hackathon de Primavera',
      eventSubtitle: 'Edición 2025',
      startsAt: '2025-05-10',
      endsAt: '2025-05-12',
    })
  })

  it('trims rating comments and sends blank ones as none', () => {
    expect(
      toSaveEventRatingRequest({
        score: 4,
        mostLiked: ' Todo ',
        leastLiked: '  ',
        suggestions: '',
      }),
    ).toEqual({ score: 4, mostLiked: 'Todo', leastLiked: null, suggestions: null })
  })

  it('builds the registration of an adult with minors', () => {
    const adult = {
      firstName: 'Ada',
      lastName: 'Lovelace',
      gender: 'Female',
      email: 'ada@example.test',
      phone: '600000000',
      secondaryPhone: null,
      nationalId: '12345678Z',
      newsConsent: false,
    } as const

    expect(
      toRegisterRequest({
        adult,
        password: 'Str0ngPass!23',
        declaresAdult: true,
        minors: [MINOR],
      }),
    ).toEqual({
      ...adult,
      password: 'Str0ngPass!23',
      declaresAdult: true,
      minors: [{ ...MINOR, declaresGuardianship: true }],
    })
  })
})

describe('toPersonalDataFile', () => {
  it('names every field in Spanish and labels the codes as the site does', () => {
    const file = toPersonalDataFile(buildPersonalDataResponse(), t)

    expect(file.generadoEl).toBe('2026-10-18T10:00:00Z')
    expect(file.cuenta).toEqual({
      id: 'user-1',
      nombre: 'Ada',
      apellidos: 'Lovelace',
      email: 'ada@example.test',
      emailNuevoSinConfirmar: 'ada@new.test',
      telefono: '600000000',
      segundoTelefono: null,
      dniNie: '12345678Z',
      genero: t('entities.user.gender.Female'),
      aceptaNovedades: true,
      relacionConLaAsociacion: 'Socio',
      estado: 'Activa',
      administraLaWeb: false,
      verificacionEnDosPasos: t('pages.account.twoFactor.methods.Authenticator'),
      altaEl: '2026-01-01T00:00:00Z',
      modificadaEl: null,
      ultimoAccesoEl: '2026-10-18T09:00:00Z',
      declaraSerMayorDeEdadEl: '2026-01-01T00:00:00Z',
    })
    expect(file.cambiosDeLaPreferenciaSobreNovedades).toEqual([
      {
        acepta: true,
        medio: t('pages.account.personalData.sources.Registration'),
        fecha: '2026-01-01T00:00:00Z',
      },
      { acepta: false, medio: 'Imported', fecha: '2026-02-01T00:00:00Z' },
    ])
    expect(file.menores).toEqual([
      {
        id: 'child-1',
        nombre: 'Byron',
        apellidos: 'Lovelace',
        fechaDeNacimiento: '2015-03-02',
        genero: t('entities.user.gender.Male'),
        relacionConLaAsociacion: 'Participante',
        altaEl: '2026-01-01T00:00:00Z',
        modificadoEl: null,
        declarasSerSuPadreMadreOTutorEl: '2026-01-01T00:00:00Z',
        teAvisamosDeQueCumple18El: '2026-06-01T00:00:00Z',
      },
    ])
    expect(file.inscripciones).toEqual([
      {
        personaId: 'child-1',
        persona: 'Byron Lovelace',
        evento: 'Hackatón',
        actividad: 'Taller',
        inicioDeLaActividad: '2026-06-01T10:00:00Z',
        rol: 'Participante',
        estado: 'Confirmada',
        inscritaEl: '2026-05-01T00:00:00Z',
      },
    ])
    expect(file.decisionesSobreDocumentos).toEqual([
      {
        evento: 'Hackatón',
        documento: 'Uso de imágenes',
        aceptado: true,
        fecha: '2026-05-01T00:00:00Z',
        firmaPngBase64: 'iVBORw0KGgo=',
      },
    ])
    expect(file.sesionesAbiertas).toEqual([
      { abiertaEl: '2026-10-18T09:00:00Z', caducaEl: '2026-10-18T21:00:00Z' },
    ])
    expect(file.registroDeSeguridad).toEqual([
      {
        fecha: '2026-10-18T08:59:00Z',
        operacion: t('pages.admin.audit.actions.Login'),
        resultado: t('pages.admin.audit.outcomes.Succeeded'),
        quien: t('pages.account.personalData.actors.Anonymous'),
        ip: '203.0.113.1',
      },
      {
        fecha: '2026-10-18T09:05:00Z',
        operacion: t('pages.admin.audit.actions.ChangePassword'),
        resultado: t('pages.admin.audit.outcomes.Rejected'),
        quien: t('pages.account.personalData.actors.self'),
        ip: '203.0.113.1',
      },
      {
        fecha: '2026-10-18T09:10:00Z',
        operacion: t('pages.admin.audit.actions.ChangeUserType'),
        resultado: t('pages.admin.audit.outcomes.Succeeded'),
        quien: t('pages.account.personalData.actors.User'),
        ip: null,
      },
    ])
  })
})
