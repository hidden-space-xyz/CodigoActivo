import { describe, expect, it } from 'vitest'

import {
  toBlockedDataAccess,
  toCandidate,
  toConsultation,
  toExtractFile,
} from '@/pages/admin/blocked-data/api/mapper'

import { t } from '../../../../../support/render'

describe('blocked data mapper', () => {
  it('maps what the administrator may do', () => {
    expect(
      toBlockedDataAccess({ isPrivacyOfficer: true, usesAuthenticator: false, entities: ['User'] }),
    ).toEqual({ isPrivacyOfficer: true, usesAuthenticator: false, entities: ['User'] })
  })

  it('maps a consultation whose officer was erased', () => {
    expect(
      toConsultation({
        id: 'c1',
        officerId: 'o1',
        authority: 'Court',
        reference: 'Ref',
        openedAt: '2026-10-25T09:00:00Z',
        expiresAt: '2026-10-25T09:15:00Z',
        foundAccounts: 2,
        extracts: 1,
      }),
    ).toEqual({
      id: 'c1',
      officerName: null,
      authority: 'Court',
      reference: 'Ref',
      openedAt: '2026-10-25T09:00:00Z',
      expiresAt: '2026-10-25T09:15:00Z',
      foundAccounts: 2,
      extracts: 1,
    })
  })

  it('names a candidate and adds up its entries', () => {
    expect(
      toCandidate({
        accountId: 'a1',
        firstName: 'Marta',
        lastName: 'Miembro',
        exists: false,
        erasedAt: '2026-10-01T10:00:00Z',
        entries: [
          { entity: 'User', count: 2, firstAt: 'x', lastAt: 'y' },
          { entity: 'Assignment', count: 3, firstAt: 'x', lastAt: 'y' },
        ],
      }),
    ).toMatchObject({
      accountId: 'a1',
      name: 'Marta Miembro',
      guardianId: null,
      exists: false,
      erasedAt: '2026-10-01T10:00:00Z',
      entryCount: 5,
    })
  })

  it('builds the extract with Spanish keys and labels', () => {
    const file = toExtractFile(
      {
        generatedAt: '2026-10-25T09:05:00Z',
        authority: 'DataProtectionAuthority',
        reference: 'E/1/2026',
        accountId: 'a1',
        from: '2026-01-01',
        to: null,
        entities: ['Assignment'],
        entries: [
          {
            id: 'e1',
            entity: 'Assignment',
            occurredAt: '2026-10-01T10:00:00Z',
            readable: true,
            operation: 'Deleted',
            actorId: 'admin',
            command: 'DeleteUser',
            values: { UserId: 'a1' },
          },
          {
            id: 'e2',
            entity: 'User',
            occurredAt: '2026-10-01T10:00:00Z',
            readable: false,
            values: {},
          },
        ],
      },
      t,
    )

    expect(file).toEqual({
      generadoEl: '2026-10-25T09:05:00Z',
      destinatario: t('pages.admin.blockedData.authorities.DataProtectionAuthority'),
      referencia: 'E/1/2026',
      cuenta: 'a1',
      desde: '2026-01-01',
      hasta: null,
      tiposDeDatos: [t('pages.admin.blockedData.entities.Assignment')],
      entradas: [
        {
          id: 'e1',
          tipo: t('pages.admin.blockedData.entities.Assignment'),
          fecha: '2026-10-01T10:00:00Z',
          legible: true,
          operacion: 'Deleted',
          realizadoPor: 'admin',
          operacionDeLaAplicacion: 'DeleteUser',
          valoresAnteriores: { UserId: 'a1' },
        },
        {
          id: 'e2',
          tipo: t('pages.admin.blockedData.entities.User'),
          fecha: '2026-10-01T10:00:00Z',
          legible: false,
          operacion: null,
          realizadoPor: null,
          operacionDeLaAplicacion: null,
          valoresAnteriores: {},
        },
      ],
      datosActuales: null,
    })
  })
})
