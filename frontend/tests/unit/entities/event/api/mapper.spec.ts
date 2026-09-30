import { describe, expect, it } from 'vitest'

import {
  toCategoryTag,
  toEventDetail,
  toEventListing,
  toEventRequest,
  toEventSummary,
  toEventTermsState,
  toLeaderRosterActivity,
  toPastEventSummary,
} from '@/entities/event/api/mapper'
import type { EventStatusKind } from '@/entities/event'
import type { EventStage } from '@/shared/api/generated/models'

import {
  buildEventCategory,
  buildEventListItem,
  buildEventResponse,
  buildEventTermsLink,
  buildLeaderRosterActivity,
  buildTermsDocumentState,
} from '../../../../support/builders'

describe('event mapper', () => {
  it.each<[EventStage, EventStatusKind]>([
    ['Upcoming', 'upcoming'],
    ['EarlySignupOpen', 'earlySignupOpen'],
    ['SignupOpen', 'signupOpen'],
    ['SignupClosed', 'signupClosed'],
    ['Finished', 'finished'],
  ])('takes the %s stage the API decided as the status', (stage, status) => {
    expect(toEventSummary(buildEventListItem({ stage })).status).toBe(status)
  })

  it('maps a list item to a card model keeping its ISO days', () => {
    const event = toEventSummary(
      buildEventListItem({
        id: 'event-1',
        title: 'Día Código Activo',
        subtitle: 'Programa tu futuro',
        eventStartsAt: '2026-10-03',
        eventEndsAt: '2026-10-04',
        thumbnailId: 'thumb-1',
        categories: [buildEventCategory({ categoryTypeId: 'cat-1', name: 'Robótica' })],
        stage: 'Upcoming',
      }),
    )

    expect(event).toEqual({
      id: 'event-1',
      title: 'Día Código Activo',
      subtitle: 'Programa tu futuro',
      startsAt: '2026-10-03',
      endsAt: '2026-10-04',
      status: 'upcoming',
      thumbnailId: 'thumb-1',
      categories: [{ id: 'cat-1', name: 'Robótica', color: '#ff6600' }],
    })
  })

  it('shows a past event as finished whatever its stage', () => {
    const event = toPastEventSummary(buildEventListItem({ id: 'event-0', stage: 'SignupClosed' }))

    expect(event).toMatchObject({ id: 'event-0', status: 'finished' })
  })

  it('maps a category type to a tag', () => {
    expect(toCategoryTag({ id: 'cat-1', name: 'IA', color: '#123456' })).toEqual({
      id: 'cat-1',
      name: 'IA',
      color: '#123456',
    })
  })

  it('maps the whole event with its signup window and terms', () => {
    const detail = toEventDetail(
      buildEventResponse({
        id: 'event-1',
        title: 'Día',
        subtitle: 'Sub',
        description: '<p>Texto</p>',
        eventStartsAt: '2026-10-03',
        eventEndsAt: '2026-10-04',
        signupStartsAt: '2026-09-10T10:00:00Z',
        signupEndsAt: '2026-09-30T10:00:00Z',
        thumbnailId: 'thumb-1',
        categories: [buildEventCategory({ categoryTypeId: 'cat-1', name: 'IA', color: '#123456' })],
        termsDocuments: [buildEventTermsLink({ name: 'Normas' })],
        stage: 'SignupOpen',
      }),
    )

    expect(detail).toEqual({
      id: 'event-1',
      title: 'Día',
      subtitle: 'Sub',
      description: '<p>Texto</p>',
      startsAt: '2026-10-03',
      endsAt: '2026-10-04',
      signupStartsAt: '2026-09-10T10:00:00Z',
      signupEndsAt: '2026-09-30T10:00:00Z',
      earlySignupStartsAt: null,
      status: 'signupOpen',
      thumbnailId: 'thumb-1',
      categories: [{ id: 'cat-1', name: 'IA', color: '#123456' }],
      terms: [{ id: 'terms-1', name: 'Normas', required: true, displayOrder: 0 }],
    })
  })

  it('keeps the early signup start of the event', () => {
    const detail = toEventDetail(
      buildEventResponse({ earlySignupStartsAt: '2026-09-15T10:00:00Z', stage: 'EarlySignupOpen' }),
    )

    expect(detail).toMatchObject({
      earlySignupStartsAt: '2026-09-15T10:00:00Z',
      status: 'earlySignupOpen',
    })
  })

  it('sorts the terms state by display order and keeps undecided documents as null', () => {
    const state = toEventTermsState({
      documents: [
        buildTermsDocumentState({
          termsDocumentId: 'terms-2',
          name: 'Imagen',
          required: false,
          displayOrder: 1,
          description: '<p>Fotos</p>',
          accepted: true,
          decidedAt: '2026-09-01T10:00:00Z',
        }),
        buildTermsDocumentState({ termsDocumentId: 'terms-1', description: '<p>Normas</p>' }),
      ],
      signupBlocked: true,
    })

    expect(state).toEqual({
      documents: [
        {
          id: 'terms-1',
          name: 'Normas del campamento',
          description: '<p>Normas</p>',
          required: true,
          displayOrder: 0,
          accepted: null,
          decidedAt: null,
        },
        {
          id: 'terms-2',
          name: 'Imagen',
          description: '<p>Fotos</p>',
          required: false,
          displayOrder: 1,
          accepted: true,
          decidedAt: '2026-09-01T10:00:00Z',
        },
      ],
      signupBlocked: true,
    })
  })

  it('maps a led activity keeping the order of roles and attendees', () => {
    const activity = toLeaderRosterActivity(buildLeaderRosterActivity())

    expect(activity).toMatchObject({
      id: 'act-1',
      title: 'Taller de robótica',
      location: 'Aula 3',
      startsAt: '2099-06-10T09:00:00Z',
      endsAt: '2099-06-10T11:00:00Z',
    })
    expect(activity.roles.map((role) => [role.id, role.name])).toEqual([
      ['role-leader', 'Líder'],
      ['role-volunteer', 'Voluntario'],
      ['role-participant', 'Participante'],
    ])
    expect(activity.roles[2]).toEqual({
      id: 'role-participant',
      name: 'Participante',
      users: [
        {
          firstName: 'Ana',
          lastName: 'Álvarez',
          email: 'ana@example.test',
          phone: '600 333 444',
          signedUpAt: '2099-05-04T10:00:00Z',
        },
      ],
      dependents: [
        {
          firstName: 'Nora',
          lastName: 'Gil',
          age: 11,
          guardian: {
            firstName: 'Gabriela',
            lastName: 'Gil',
            email: 'gabriela@example.test',
            phone: '600 555 666',
          },
          signedUpAt: '2099-05-05T10:00:00Z',
        },
        {
          firstName: 'Hugo',
          lastName: 'Gil',
          age: 1,
          guardian: { firstName: 'Gabriela', lastName: 'Gil', email: '', phone: '' },
          signedUpAt: '2099-05-06T10:00:00Z',
        },
      ],
    })
  })

  it('leaves missing contact data and ages empty in the roster', () => {
    const activity = toLeaderRosterActivity(
      buildLeaderRosterActivity({
        roles: [
          {
            roleTypeId: 'role-participant',
            roleName: 'Participante',
            users: [
              {
                firstName: 'Ana',
                lastName: 'Álvarez',
                email: null,
                phone: null,
                signedUpAt: '2099-05-04T10:00:00Z',
              },
            ],
            dependents: [
              {
                firstName: 'Nora',
                lastName: 'Gil',
                age: null,
                guardian: { firstName: 'Gabriela', lastName: 'Gil', email: null, phone: null },
                signedUpAt: '2099-05-05T10:00:00Z',
              },
            ],
          },
        ],
      }),
    )

    expect(activity.roles[0]?.users[0]).toMatchObject({ email: '', phone: '' })
    expect(activity.roles[0]?.dependents[0]).toMatchObject({
      age: null,
      guardian: { email: '', phone: '' },
    })
  })
})

describe('event admin mapper', () => {
  it('maps a list item to an admin table row', () => {
    expect(
      toEventListing(
        buildEventListItem({
          id: 'event-1',
          featured: true,
          signupStartsAt: '2026-09-01T08:00:00Z',
          signupEndsAt: '2026-10-01T20:00:00Z',
          earlySignupStartsAt: '2026-08-20T08:00:00Z',
        }),
      ),
    ).toMatchObject({
      id: 'event-1',
      featured: true,
      signupStartsAt: '2026-09-01T08:00:00Z',
      signupEndsAt: '2026-10-01T20:00:00Z',
      earlySignupStartsAt: '2026-08-20T08:00:00Z',
    })
    expect(toEventListing(buildEventListItem()).earlySignupStartsAt).toBeNull()
  })

  it('builds the body that saves an event, without terms when it has none', () => {
    const input = {
      title: 'Día',
      subtitle: 'Sub',
      description: '<p>Texto</p>',
      startsAt: '2026-10-03',
      endsAt: '2026-10-04',
      earlySignupStartsAt: null,
      signupStartsAt: '2026-09-10T10:00:00Z',
      signupEndsAt: '2026-09-30T10:00:00Z',
      thumbnailId: 'thumb-1',
      categoryIds: ['cat-1'],
      terms: [{ documentId: 'terms-1', required: true }],
    }

    expect(toEventRequest(input)).toEqual({
      title: 'Día',
      subtitle: 'Sub',
      description: '<p>Texto</p>',
      eventStartsAt: '2026-10-03',
      eventEndsAt: '2026-10-04',
      earlySignupStartsAt: null,
      signupStartsAt: '2026-09-10T10:00:00Z',
      signupEndsAt: '2026-09-30T10:00:00Z',
      thumbnailId: 'thumb-1',
      categoryTypeIds: ['cat-1'],
      termsDocuments: [{ termsDocumentId: 'terms-1', required: true }],
    })
    expect(toEventRequest({ ...input, terms: [] }).termsDocuments).toBeNull()
  })
})
