import { describe, expect, it } from 'vitest'

import {
  readEventDraft,
  toEventDraft,
  type EventDraft,
} from '@/pages/admin/events/model/event-form'
import { EMPTY_DOC_JSON } from '@/shared/lib/rich-text'

import { richText } from '../../../../../support/builders'
import { buildEventDetail } from '../../../../../support/models'

function validDraft(overrides: Partial<EventDraft> = {}): EventDraft {
  return {
    title: ' Hackathon ',
    subtitle: ' Code all night ',
    description: richText('Bring a laptop.'),
    categoryIds: ['cat-1'],
    termsDocumentIds: ['terms-2', 'terms-1'],
    termsRequired: { 'terms-2': false },
    startsAt: new Date(2026, 9, 10),
    endsAt: new Date(2026, 9, 12),
    earlySignupStartsAt: null,
    signupStartsAt: new Date(2026, 8, 1, 9, 0),
    signupEndsAt: new Date(2026, 9, 1, 20, 0),
    ...overrides,
  }
}

describe('toEventDraft', () => {
  it('starts blank', () => {
    expect(toEventDraft(null)).toEqual({
      title: '',
      subtitle: '',
      description: '',
      categoryIds: [],
      termsDocumentIds: [],
      termsRequired: {},
      startsAt: null,
      endsAt: null,
      earlySignupStartsAt: null,
      signupStartsAt: null,
      signupEndsAt: null,
    })
  })

  it('fills the draft from the edited event, its terms documents in display order', () => {
    const draft = toEventDraft(
      buildEventDetail({
        earlySignupStartsAt: '2026-08-20T08:00:00.000Z',
        terms: [
          { id: 'terms-2', name: 'Imagen', required: false, displayOrder: 1 },
          { id: 'terms-1', name: 'Normas', required: true, displayOrder: 0 },
        ],
      }),
    )

    expect(draft).toEqual({
      title: 'Día Código Activo',
      subtitle: 'Programa tu futuro',
      description: richText('Un día de código.'),
      categoryIds: ['cat-1'],
      termsDocumentIds: ['terms-1', 'terms-2'],
      termsRequired: { 'terms-1': true, 'terms-2': false },
      startsAt: new Date(2026, 9, 3),
      endsAt: new Date(2026, 9, 4),
      earlySignupStartsAt: new Date('2026-08-20T08:00:00.000Z'),
      signupStartsAt: new Date('2026-09-01T08:00:00.000Z'),
      signupEndsAt: new Date('2026-10-01T20:00:00.000Z'),
    })
  })
})

describe('readEventDraft', () => {
  it('reads a valid draft into the event to save', () => {
    expect(readEventDraft(validDraft())).toEqual({
      problems: {},
      value: {
        title: 'Hackathon',
        subtitle: 'Code all night',
        description: richText('Bring a laptop.'),
        startsAt: '2026-10-10',
        endsAt: '2026-10-12',
        earlySignupStartsAt: null,
        signupStartsAt: new Date(2026, 8, 1, 9, 0).toISOString(),
        signupEndsAt: new Date(2026, 9, 1, 20, 0).toISOString(),
        categoryIds: ['cat-1'],
        terms: [
          { documentId: 'terms-2', required: false },
          { documentId: 'terms-1', required: true },
        ],
      },
    })
  })

  it('saves an empty description as an empty document and keeps the early signup', () => {
    const early = new Date(2026, 7, 20, 8, 0)

    expect(
      readEventDraft(validDraft({ description: ' ', earlySignupStartsAt: early })).value,
    ).toMatchObject({ description: EMPTY_DOC_JSON, earlySignupStartsAt: early.toISOString() })
  })

  it('refuses every missing field', () => {
    expect(readEventDraft(toEventDraft(null))).toEqual({
      problems: {
        title: true,
        subtitle: true,
        categoryIds: 'pages.admin.events.form.problems.categoriesRequired',
        startsAt: 'pages.admin.events.form.problems.eventStartRequired',
        endsAt: 'pages.admin.events.form.problems.eventEndRequired',
        signupStartsAt: 'pages.admin.events.form.problems.signupStartRequired',
        signupEndsAt: 'pages.admin.events.form.problems.signupEndRequired',
      },
      value: null,
    })
  })

  it('refuses dates in the wrong order', () => {
    const reading = readEventDraft(
      validDraft({
        startsAt: new Date(2026, 9, 12),
        endsAt: new Date(2026, 9, 10),
        signupStartsAt: new Date(2026, 9, 20, 10, 0),
        signupEndsAt: new Date(2026, 9, 20, 10, 0),
        earlySignupStartsAt: new Date(2026, 9, 21, 10, 0),
      }),
    )

    expect(reading).toEqual({
      problems: {
        endsAt: 'pages.admin.events.form.problems.eventOrderInvalid',
        signupStartsAt: 'pages.admin.events.form.problems.signupAfterEventEnd',
        signupEndsAt: 'pages.admin.events.form.problems.signupOrderInvalid',
        earlySignupStartsAt: 'pages.admin.events.form.problems.earlySignupOrderInvalid',
      },
      value: null,
    })
  })

  it('lets signup open on the last day of the event', () => {
    const reading = readEventDraft(
      validDraft({
        signupStartsAt: new Date(2026, 9, 12, 18, 0),
        signupEndsAt: new Date(2026, 9, 12, 20, 0),
      }),
    )

    expect(reading.problems).toEqual({})
  })
})
