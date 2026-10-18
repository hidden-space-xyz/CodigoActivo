import { describe, expect, it } from 'vitest'

import { toAuditEntry } from '@/pages/admin/audit/api/mapper'
import {
  AUDIT_ACTIONS,
  AUDIT_ACTOR_TYPES,
  AUDIT_ENTITY_TYPES,
  AUDIT_OUTCOMES,
  actionLabelKey,
  actorLabel,
  actorTypeLabelKey,
  entityLabel,
  entityTypeLabelKey,
  errorCodeLabel,
  outcomeLabelKey,
  outcomeTagType,
  shortId,
} from '@/pages/admin/audit/lib/labels'
import { AuditAction } from '@/shared/api/generated/models'
import { i18n } from '@/shared/i18n'

import { buildAuditEntry } from '../../../../../support/builders'
import { t } from '../../../../../support/render'

function has(key: string): boolean {
  return i18n.global.te(key)
}

describe('audit labels', () => {
  it('lists and names every command the API declares', () => {
    expect([...AUDIT_ACTIONS].sort()).toEqual(Object.values(AuditAction).sort())
    for (const action of AUDIT_ACTIONS) expect(has(actionLabelKey(action))).toBe(true)
    expect(t(actionLabelKey('SetAdmin'))).toBe('Cambiar los permisos de administrador')
  })

  it('names every actor kind, entity kind and outcome', () => {
    for (const actorType of AUDIT_ACTOR_TYPES) expect(has(actorTypeLabelKey(actorType))).toBe(true)
    for (const entityType of AUDIT_ENTITY_TYPES) {
      expect(has(entityTypeLabelKey(entityType))).toBe(true)
    }
    expect(AUDIT_OUTCOMES.map((outcome) => t(outcomeLabelKey(outcome)))).toEqual([
      'Correcto',
      'Rechazado',
      'Fallido',
    ])
    expect(AUDIT_OUTCOMES.map(outcomeTagType)).toEqual(['success', 'warning', 'danger'])
  })

  it('names the actor by its account, an erased account, or its kind', () => {
    expect(actorLabel(toAuditEntry(buildAuditEntry()), t)).toBe('Ada Admin')
    expect(actorLabel(toAuditEntry(buildAuditEntry({ actorName: null })), t)).toBe(
      'Cuenta eliminada',
    )
    expect(
      actorLabel(toAuditEntry(buildAuditEntry({ actorType: 'System', actorId: null })), t),
    ).toBe('Sistema')
    expect(
      actorLabel(toAuditEntry(buildAuditEntry({ actorType: 'Anonymous', actorId: null })), t),
    ).toBe('Anónimo')
  })

  it('names an entity by its user or by its kind and short identifier', () => {
    expect(shortId('aaaaaaaa-1111-1111-1111-111111111111')).toBe('aaaaaaaa')
    expect(entityLabel({ type: 'User', id: 'user-2', name: 'Marta Miembro' }, t)).toBe(
      'Marta Miembro',
    )
    expect(
      entityLabel({ type: 'Event', id: 'bbbbbbbb-2222-2222-2222-222222222222', name: '' }, t),
    ).toBe('Evento bbbbbbbb')
  })

  it('explains a refusal with the message the user saw, its own label or the bare code', () => {
    expect(errorCodeLabel('InvalidCredentials', t, has)).toBe(t('errors.InvalidCredentials'))
    expect(errorCodeLabel('UniqueConstraintViolation', t, has)).toBe(
      'Otra petición guardó el mismo dato a la vez.',
    )
    expect(errorCodeLabel('SomethingNew', t, has)).toBe('SomethingNew')
  })
})
