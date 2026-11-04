import http from 'k6/http'
import { fail } from 'k6'
import { body } from './http.js'
import { activityBody, decisionsFor, eventBody, LOOKUPS, richText } from './data.js'

const RUSH_ACTIVITIES = 8
const ROSTER_SIZE = 20

const roleCapacities = [
  { activityRoleTypeId: LOOKUPS.roles.participant, desiredCount: 500 },
  { activityRoleTypeId: LOOKUPS.roles.volunteer, desiredCount: 50 },
  { activityRoleTypeId: LOOKUPS.roles.leader, desiredCount: 5 },
]

function create(session, path, options, what) {
  const res = session.post(path, { ...options, expect: [201] })
  if (res.status !== 201) {
    fail(`Creating ${what} answered ${res.status}: ${res.body}`)
  }
  return body(res)
}

function list(session, path, query) {
  return body(session.get(path, { query }))?.items ?? []
}

function termsDocument(session, kind, requiresSignature, label) {
  return create(
    session,
    '/api/events/termsDocument',
    {
      json: {
        name: `${label} ${Date.now().toString(36)}`,
        description: richText(`${label} de la suite de rendimiento.`),
        kind,
        requiresSignature,
      },
    },
    label,
  )
}

export function uploadImage(session, image) {
  return session.post('/api/files', {
    form: { file: http.file(image, 'rendimiento.webp', 'image/webp') },
    expect: [201],
  })
}

export function downloadImage(session, fileId) {
  return session.get('/api/files/{fileId}/content', { route: { fileId }, responseType: 'binary' })
    .body
}

export function createFixtures(admin, runId) {
  const { session } = admin
  const [demoEvent] = list(session, '/api/events', { pageSize: 1 })
  const sourceFileId = demoEvent.thumbnailId
  const thumbnailId = body(uploadImage(session, downloadImage(session, sourceFileId)))?.id
  if (!thumbnailId) {
    fail('Uploading the thumbnail failed')
  }
  const category = create(
    session,
    '/api/events/categoryType',
    { json: { name: `Rendimiento ${runId}`, color: '#2DD4D9' } },
    'the category',
  )
  const conditions = termsDocument(session, 'Conditions', true, 'Condiciones')
  const consent = termsDocument(session, 'Consent', false, 'Consentimiento de imagen')
  const rush = create(
    session,
    '/api/events',
    {
      json: eventBody({
        thumbnailId,
        categoryIds: [category.id],
        terms: [
          { termsDocumentId: conditions.id, required: true },
          { termsDocumentId: consent.id, required: false },
        ],
        title: `Jornada de inscripción ${runId}`,
      }),
    },
    'the signup event',
  )
  const activities = Array.from(
    { length: RUSH_ACTIVITIES },
    (_, slot) =>
      create(
        session,
        '/api/activities/{eventId}',
        {
          route: { eventId: rush.id },
          json: activityBody({
            thumbnailId,
            modalityId: LOOKUPS.modalities.onSite,
            roles: roleCapacities,
            slot,
          }),
        },
        'a signup activity',
      ).id,
  )
  const news = create(
    session,
    '/api/news',
    {
      json: {
        title: `Abiertas las inscripciones ${runId}`,
        subtitle: 'Noticia fija de la suite de rendimiento',
        description: richText('Ya puedes inscribirte en la jornada.', thumbnailId),
        thumbnailId,
      },
    },
    'the news item',
  )
  const [featuredEvent] = list(session, '/api/events', { featured: true, pageSize: 1 })
  const [featuredNews] = list(session, '/api/news', { featured: true, pageSize: 1 })
  const pastEvents = list(session, '/api/events', { scope: 'Past', pageSize: 20 }).map(
    (event) => event.id,
  )
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
  const [activityId] = fixtures.activities
  const { documents } = body(
    roster[0].session.get('/api/events/{eventId}/terms', {
      route: { eventId: fixtures.rushEventId },
    }),
  )
  const termsDecisions = decisionsFor(documents)
  for (const { session, dependents } of roster) {
    session.post('/api/activities/{activityId}/assign-household', {
      route: { activityId },
      json: {
        assignments: [session.userId, ...dependents].map((userId) => ({
          userId,
          activityRoleTypeId: LOOKUPS.roles.participant,
          termsDecisions,
        })),
      },
    })
    admin.session.patch('/api/activities/{activityId}/{userId}/change-status', {
      route: { activityId, userId: session.userId },
      json: { assignmentStatusId: LOOKUPS.statuses.confirmed },
    })
  }
  admin.session.patch('/api/activities/{activityId}/{userId}/change-role', {
    route: { activityId, userId: roster[0].session.userId },
    json: { activityRoleTypeId: LOOKUPS.roles.leader },
  })
  return roster.map(({ session, dependents }) => ({ id: session.userId, dependents }))
}
