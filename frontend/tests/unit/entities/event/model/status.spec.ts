import { describe, expect, it } from 'vitest'

import {
  signupAccess,
  statusLabelKey,
  type EventStatusKind,
  type SignupAccess,
} from '@/entities/event'

import { t } from '../../../../support/render'

describe('statusLabelKey', () => {
  it.each<[EventStatusKind, string]>([
    ['upcoming', 'Próximamente'],
    ['earlySignupOpen', 'Inscripción anticipada'],
    ['signupOpen', 'Inscripción abierta'],
    ['signupClosed', 'Inscripción cerrada'],
    ['finished', 'Finalizado'],
  ])('labels the %s stage', (status, label) => {
    expect(t(statusLabelKey(status))).toBe(label)
  })
})

describe('signupAccess', () => {
  it.each<[EventStatusKind, boolean, SignupAccess]>([
    ['signupOpen', false, 'open'],
    ['signupOpen', true, 'open'],
    ['earlySignupOpen', true, 'open'],
    ['earlySignupOpen', false, 'earlyOnly'],
    ['upcoming', true, 'closed'],
    ['signupClosed', true, 'closed'],
    ['finished', true, 'closed'],
  ])('gives %s with eligibility %s the %s access', (status, eligible, access) => {
    expect(signupAccess({ status }, eligible)).toBe(access)
  })
})
