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

function create(session, path, options) {
  return body(session.post(path, { ...options, expect: [201] }))
}

function list(session, path, query) {
  return body(session.get(path, { query })).items
}

function termsDocument(session, kind, requiresSignature, label) {
  return create(session, '/api/events/termsDocument', {
    json: {
      name: `${label} ${Date.now().toString(36)}`,
      description: richText(`${label} de la suite de rendimiento.`),
      kind,
      requiresSignature,
    },
  })
}

/** Uploads an image the way the file field of the back office does. */
export function uploadImage(session, image) {
  return session.post('/api/files', {
    form: { file: http.file(image, 'rendimiento.webp', 'image/webp') },
    expect: [201],
  })
}

/** Reads a stored image, which the upload scenario sends back as new files. */
export function downloadImage(session, fileId) {
  return session.get('/api/files/{fileId}/content', { route: { fileId }, responseType: 'binary' })
    .body
}

/**
 * Creates what the scenarios share: the event of the signup rush, its activities, terms and news.
 */
export function createFixtures(admin, runId) {
  const { session } = admin
  const [demoEvent] = list(session, '/api/events', { pageSize: 1 })
  const sourceFileId = demoEvent.thumbnailId
  const thumbnailId = body(uploadImage(session, downloadImage(session, sourceFileId))).id
  const category = create(session, '/api/events/categoryType', {
    json: { name: `Rendimiento ${runId}`, color: '#2DD4D9' },
  })
  const conditions = termsDocument(session, 'Conditions', true, 'Condiciones')
  const consent = termsDocument(session, 'Consent', false, 'Consentimiento de imagen')
  const rush = create(session, '/api/events', {
    json: eventBody({
      thumbnailId,
      categoryIds: [category.id],
      terms: [
        { termsDocumentId: conditions.id, required: true },
        { termsDocumentId: consent.id, required: false },
      ],
      title: `Jornada de inscripción ${runId}`,
    }),
  })
  const activities = Array.from(
    { length: RUSH_ACTIVITIES },
    (_, slot) =>
      create(session, '/api/activities/{eventId}', {
        route: { eventId: rush.id },
        json: activityBody({
          thumbnailId,
          modalityId: LOOKUPS.modalities.onSite,
          roles: roleCapacities,
          slot,
        }),
      }).id,
  )
  const news = create(session, '/api/news', {
    json: {
      title: `Abiertas las inscripciones ${runId}`,
      subtitle: 'Noticia fija de la suite de rendimiento',
      description: richText('Ya puedes inscribirte en la jornada.', thumbnailId),
      thumbnailId,
    },
  })
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

/** Signs households up for the first activity of the rush, which the rush itself leaves alone. */
export function seedSignups(admin, members, fixtures) {
  const roster = members.filter(({ dependents }) => dependents.length).slice(0, ROSTER_SIZE)
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
