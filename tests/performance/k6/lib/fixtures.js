import http from 'k6/http'
import { fail } from 'k6'
import { body, query } from './http.js'
import { activityBody, CATALOG, decisionsFor, eventBody, richText } from './data.js'

const RUSH_ACTIVITIES = 8
const ROSTER_SIZE = 20

const roleCapacities = [
  { activityRoleTypeId: CATALOG.roles.participant, desiredCount: 500 },
  { activityRoleTypeId: CATALOG.roles.volunteer, desiredCount: 50 },
  { activityRoleTypeId: CATALOG.roles.leader, desiredCount: 5 },
]

function created(res, what) {
  if (res.status !== 201) {
    fail(`Creating ${what} answered ${res.status}: ${res.body}`)
  }
  return body(res)
}

export function uploadImage(session, image) {
  return session.post('/api/files', {
    name: 'POST /api/files',
    form: { file: http.file(image, 'rendimiento.webp', 'image/webp') },
    expect: [201],
  })
}

export function downloadImage(session, fileId) {
  return session.get(`/api/files/${fileId}/content`, {
    name: 'GET /api/files/{fileId}/content',
    responseType: 'binary',
  }).body
}

export function termsDocument(session, kind, requiresSignature, label) {
  return session.post('/api/events/termsDocument', {
    name: 'POST /api/events/termsDocument',
    json: {
      name: `${label} ${Date.now().toString(36)}`,
      description: richText(`${label} de la suite de rendimiento.`),
      kind,
      requiresSignature,
    },
    expect: [201],
  })
}

export function createFixtures(admin, runId) {
  const session = admin.session
  const [demoEvent] = body(
    session.get(`/api/events${query({ pageSize: 1 })}`, { name: 'GET /api/events' }),
  ).items
  const sourceFileId = demoEvent.thumbnailId
  const thumbnailId = created(
    uploadImage(session, downloadImage(session, sourceFileId)),
    'the thumbnail',
  ).id
  const category = created(
    session.post('/api/events/categoryType', {
      name: 'POST /api/events/categoryType',
      json: { name: `Rendimiento ${runId}`, color: '#2DD4D9' },
      expect: [201],
    }),
    'the category',
  )
  const conditions = created(
    termsDocument(session, 'Conditions', true, 'Condiciones'),
    'the conditions',
  )
  const consent = created(
    termsDocument(session, 'Consent', false, 'Consentimiento de imagen'),
    'the consent',
  )
  const rush = created(
    session.post('/api/events', {
      name: 'POST /api/events',
      json: eventBody({
        thumbnailId,
        categoryIds: [category.id],
        terms: [
          { termsDocumentId: conditions.id, required: true },
          { termsDocumentId: consent.id, required: false },
        ],
        title: `Jornada de inscripción ${runId}`,
      }),
      expect: [201],
    }),
    'the signup event',
  )
  const activities = Array.from(
    { length: RUSH_ACTIVITIES },
    (_, slot) =>
      created(
        session.post(`/api/activities/${rush.id}`, {
          name: 'POST /api/activities/{eventId}',
          json: activityBody({
            thumbnailId,
            modalityId: CATALOG.modalities.onSite,
            roles: roleCapacities,
            slot,
          }),
          expect: [201],
        }),
        'a signup activity',
      ).id,
  )
  const news = created(
    session.post('/api/news', {
      name: 'POST /api/news',
      json: {
        title: `Abiertas las inscripciones ${runId}`,
        subtitle: 'Noticia fija de la suite de rendimiento',
        description: richText('Ya puedes inscribirte en la jornada.', thumbnailId),
        thumbnailId,
      },
      expect: [201],
    }),
    'the news item',
  )
  const events = (criteria) =>
    body(session.get(`/api/events${query(criteria)}`, { name: 'GET /api/events' })).items
  const [featuredEvent] = events({ featured: true, pageSize: 1 })
  const [featuredNews] = body(
    session.get(`/api/news${query({ featured: true, pageSize: 1 })}`, { name: 'GET /api/news' }),
  ).items
  const pastEvents = events({ scope: 'Past', pageSize: 20 }).map((event) => event.id)
  if (!pastEvents.length) {
    fail('The demo data holds no past events')
  }
  return {
    sourceFileId,
    thumbnailId,
    categoryId: category.id,
    conditionsId: conditions.id,
    consentId: consent.id,
    rushEventId: rush.id,
    activities,
    newsId: news.id,
    featuredEventId: featuredEvent?.id ?? rush.id,
    featuredNewsId: featuredNews?.id ?? news.id,
    pastEvents,
  }
}

export function seedSignups(admin, members, fixtures) {
  const roster = members.slice(0, ROSTER_SIZE)
  const [rosterActivity] = fixtures.activities
  const documents = body(
    roster[0].session.get(`/api/events/${fixtures.rushEventId}/terms`, {
      name: 'GET /api/events/{eventId}/terms',
    }),
  ).documents
  const decisions = decisionsFor(documents)
  for (const member of roster) {
    const people = [member.session.userId, ...member.dependents]
    member.session.post(`/api/activities/${rosterActivity}/assign-household`, {
      name: 'POST /api/activities/{activityId}/assign-household',
      json: {
        assignments: people.map((userId) => ({
          userId,
          activityRoleTypeId: CATALOG.roles.participant,
          termsDecisions: decisions,
        })),
      },
    })
    admin.session.patch(
      `/api/activities/${rosterActivity}/${member.session.userId}/change-status`,
      {
        name: 'PATCH /api/activities/{activityId}/{userId}/change-status',
        json: { assignmentStatusId: CATALOG.statuses.confirmed },
      },
    )
  }
  admin.session.patch(`/api/activities/${rosterActivity}/${roster[0].session.userId}/change-role`, {
    name: 'PATCH /api/activities/{activityId}/{userId}/change-role',
    json: { activityRoleTypeId: CATALOG.roles.leader },
  })
  return roster.map((member) => ({ id: member.session.userId, dependents: member.dependents }))
}
