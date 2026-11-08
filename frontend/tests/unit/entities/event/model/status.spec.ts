import { describe, expect, it } from 'vitest'

import {
  nextSignupChange,
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

describe('nextSignupChange', () => {
  const signupWindow = {
    earlySignupStartsAt: '2026-09-01T08:00:00Z',
    signupStartsAt: '2026-09-02T08:00:00Z',
    signupEndsAt: '2026-09-30T20:00:00Z',
  }

  it.each<[string, string | null]>([
    ['2026-08-31T12:00:00Z', '2026-09-01T08:00:00Z'],
    ['2026-09-01T08:00:00Z', '2026-09-02T08:00:00Z'],
    ['2026-09-10T12:00:00Z', '2026-09-30T20:00:00Z'],
    ['2026-09-30T20:00:00Z', null],
  ])('at %s gives %s', (now, change) => {
    expect(nextSignupChange(signupWindow, Date.parse(now))).toBe(change && Date.parse(change))
  })

  it('skips an early signup the event does not have', () => {
    const change = nextSignupChange(
      { ...signupWindow, earlySignupStartsAt: null },
      Date.parse('2026-08-31T12:00:00Z'),
    )

    expect(change).toBe(Date.parse('2026-09-02T08:00:00Z'))
  })
})
