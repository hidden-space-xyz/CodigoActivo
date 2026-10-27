import { body, query, think } from '../lib/http.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { activityBody, CATALOG, day, DRAFT, eventBody, pick, richText, word } from '../lib/data.js'
import { termsDocument } from '../lib/fixtures.js'

function users(session) {
  session.get(`/api/users${query({ page: 1, pageSize: 25 })}`, { name: 'GET /api/users' })
  const found = body(
    session.get(
      `/api/users${query({ scope: 'Independent', name: pick(['a', 'e', 'o']), pageSize: 25 })}`,
      {
        name: 'GET /api/users',
      },
    ),
  ).items
  session.get(
    `/api/users${query({ userTypeId: CATALOG.userTypes.member, newsConsent: true, birthDateTo: day(-6570) })}`,
    { name: 'GET /api/users' },
  )
  if (found.length) {
    session.get(`/api/users/${pick(found).id}`, { name: 'GET /api/users/{userId}' })
  }
  session.get('/api/users/types', { name: 'GET /api/users/types' })
  session.get('/api/users/status-types', { name: 'GET /api/users/status-types' })
  session.get('/api/activities/roleType', { name: 'GET /api/activities/roleType' })
  session.get('/api/activities/modality-types', { name: 'GET /api/activities/modality-types' })
  session.get('/api/activities/assignment-status-types', {
    name: 'GET /api/activities/assignment-status-types',
  })
  session.get('/api/resources/types', { name: 'GET /api/resources/types' })
}

function categories(session) {
  const color = '#F9A320'
  const created = body(
    session.post('/api/events/categoryType', {
      name: 'POST /api/events/categoryType',
      json: { name: `Categoría ${word()} ${Date.now().toString(36)}`, color },
      expect: [201],
    }),
  )
  session.put(`/api/events/categoryType/${created.id}`, {
    name: 'PUT /api/events/categoryType/{eventCategoryTypeId}',
    json: { name: `${created.name} revisada`, color: '#FF6B5E' },
  })
  session.get(`/api/events/categoryType${query({ pageSize: 25 })}`, {
    name: 'GET /api/events/categoryType',
  })
  session.delete(`/api/events/categoryType/${created.id}`, {
    name: 'DELETE /api/events/categoryType/{eventCategoryTypeId}',
    expect: [204],
  })
}

function documents(session) {
  const created = body(termsDocument(session, 'Conditions', false, 'Normas'))
  session.put(`/api/events/termsDocument/${created.id}`, {
    name: 'PUT /api/events/termsDocument/{termsDocumentId}',
    json: {
      name: `${created.name} revisadas`,
      description: richText('Normas revisadas.'),
      kind: 'Conditions',
      requiresSignature: false,
    },
  })
  session.get(`/api/events/termsDocument${query({ pageSize: 25 })}`, {
    name: 'GET /api/events/termsDocument',
  })
  session.delete(`/api/events/termsDocument/${created.id}`, {
    name: 'DELETE /api/events/termsDocument/{termsDocumentId}',
    expect: [204],
  })
}

function events(session, fixtures) {
  const draft = eventBody({
    thumbnailId: fixtures.thumbnailId,
    categoryIds: [fixtures.categoryId],
    terms: [{ termsDocumentId: fixtures.conditionsId, required: true }],
    startsIn: 30,
  })
  const created = body(
    session.post('/api/events', { name: 'POST /api/events', json: draft, expect: [201] }),
  )
  session.get(`/api/events/${created.id}`, { name: 'GET /api/events/{eventId}' })
  session.put(`/api/events/${created.id}`, {
    name: 'PUT /api/events/{eventId}',
    json: { ...draft, subtitle: 'Evento revisado por la suite de rendimiento' },
  })
  const activity = activityBody({
    thumbnailId: fixtures.thumbnailId,
    modalityId: CATALOG.modalities.online,
    roles: [{ activityRoleTypeId: CATALOG.roles.participant, desiredCount: 20 }],
    startsIn: 30,
  })
  const createdActivity = body(
    session.post(`/api/activities/${created.id}`, {
      name: 'POST /api/activities/{eventId}',
      json: activity,
      expect: [201],
    }),
  )
  session.put(`/api/activities/${createdActivity.id}`, {
    name: 'PUT /api/activities/{activityId}',
    json: { ...activity, location: 'Online' },
  })
  session.get(`/api/activities/${createdActivity.id}`, { name: 'GET /api/activities/{activityId}' })
  session.get(`/api/activities${query({ eventId: created.id })}`, { name: 'GET /api/activities' })
  session.get(`/api/activities${query({ title: word(), pageSize: 25 })}`, {
    name: 'GET /api/activities',
  })
  think()
  session.delete(`/api/activities/${createdActivity.id}`, {
    name: 'DELETE /api/activities/{activityId}',
    expect: [204],
  })
  session.delete(`/api/events/${created.id}`, {
    name: 'DELETE /api/events/{eventId}',
    expect: [204],
  })
}

function featured(session, fixtures, round) {
  const eventId = round % 2 ? fixtures.rushEventId : fixtures.featuredEventId
  session.patch(`/api/events/${eventId}/feature`, { name: 'PATCH /api/events/{eventId}/feature' })
  const newsId = round % 2 ? fixtures.newsId : fixtures.featuredNewsId
  session.patch(`/api/news/${newsId}/feature`, { name: 'PATCH /api/news/{newsItemId}/feature' })
}

function signups(session, data, round) {
  const [rosterActivity] = data.fixtures.activities
  const others = data.fixtures.roster.slice(1)
  if (!others.length) {
    return
  }
  const member = pick(others, round)
  session.patch(`/api/activities/${rosterActivity}/${member.id}/change-status`, {
    name: 'PATCH /api/activities/{activityId}/{userId}/change-status',
    json: {
      assignmentStatusId: round % 2 ? CATALOG.statuses.requested : CATALOG.statuses.confirmed,
    },
  })
  if (member.dependents.length) {
    session.patch(`/api/activities/${rosterActivity}/${member.dependents[0]}/change-role`, {
      name: 'PATCH /api/activities/{activityId}/{userId}/change-role',
      json: { activityRoleTypeId: round % 2 ? CATALOG.roles.volunteer : CATALOG.roles.participant },
    })
  }
}

function content(session, fixtures) {
  const { thumbnailId } = fixtures
  const newsDraft = {
    title: `${DRAFT} noticia de ${word()} ${Date.now().toString(36)}`,
    subtitle: 'Noticia de la suite de rendimiento',
    description: richText(`Crónica del taller de ${word()}.`, thumbnailId),
    thumbnailId,
  }
  const news = body(
    session.post('/api/news', { name: 'POST /api/news', json: newsDraft, expect: [201] }),
  )
  session.get(`/api/news/${news.id}`, { name: 'GET /api/news/{newsItemId}' })
  session.put(`/api/news/${news.id}`, {
    name: 'PUT /api/news/{newsItemId}',
    json: { ...newsDraft, subtitle: 'Noticia revisada' },
  })
  session.get(`/api/news${query({ pageSize: 25, year: new Date().getFullYear() })}`, {
    name: 'GET /api/news',
  })
  session.get('/api/news/years', { name: 'GET /api/news/years' })
  session.delete(`/api/news/${news.id}`, { name: 'DELETE /api/news/{newsItemId}', expect: [204] })
  think()

  const partnerDraft = {
    name: `${DRAFT} entidad ${word()} ${Date.now().toString(36)}`,
    fromDate: day(-400),
    tier: 1,
    website: 'https://example.org/',
    thumbnailId,
  }
  const partner = body(
    session.post('/api/partners', {
      name: 'POST /api/partners',
      json: partnerDraft,
      expect: [201],
    }),
  )
  session.get(`/api/partners/${partner.id}`, { name: 'GET /api/partners/{partnerId}' })
  session.put(`/api/partners/${partner.id}`, {
    name: 'PUT /api/partners/{partnerId}',
    json: { ...partnerDraft, tier: 2 },
  })
  session.get(`/api/partners${query({ pageSize: 25, tier: 2 })}`, { name: 'GET /api/partners' })
  session.delete(`/api/partners/${partner.id}`, {
    name: 'DELETE /api/partners/{partnerId}',
    expect: [204],
  })
  think()

  const resourceDraft = {
    title: `${DRAFT} recurso de ${word()} ${Date.now().toString(36)}`,
    subtitle: 'Recurso de la suite de rendimiento',
    description: richText(`Guía de ${word()}.`),
    url: null,
    resourceTypeId: CATALOG.resourceTypes.internal,
    thumbnailId,
  }
  const resource = body(
    session.post('/api/resources', {
      name: 'POST /api/resources',
      json: resourceDraft,
      expect: [201],
    }),
  )
  session.get(`/api/resources/${resource.id}`, { name: 'GET /api/resources/{resourceId}' })
  session.put(`/api/resources/${resource.id}`, {
    name: 'PUT /api/resources/{resourceId}',
    json: {
      ...resourceDraft,
      description: null,
      url: 'https://example.org/guia',
      resourceTypeId: CATALOG.resourceTypes.external,
    },
  })
  session.get(
    `/api/resources${query({ pageSize: 25, resourceTypeId: CATALOG.resourceTypes.external })}`,
    {
      name: 'GET /api/resources',
    },
  )
  session.delete(`/api/resources/${resource.id}`, {
    name: 'DELETE /api/resources/{resourceId}',
    expect: [204],
  })
}

function legal(session, round) {
  const kind = round % 2 ? 'Cookies' : 'Privacy'
  session.put(`/api/legal-policies/${kind}`, {
    name: 'PUT /api/legal-policies/{kind}',
    json: { content: richText(`Política revisada el ${new Date().toISOString()}.`) },
  })
  const versions = body(
    session.get(`/api/legal-policies/${kind}/versions`, {
      name: 'GET /api/legal-policies/{kind}/versions',
    }),
  )
  if (versions.length) {
    session.get(`/api/legal-policies/${kind}/versions/${pick(versions).version}`, {
      name: 'GET /api/legal-policies/{kind}/versions/{version}',
    })
  }
  session.get(`/api/legal-policies/${kind}`, { name: 'GET /api/legal-policies/{kind}' })
}

function decisions(session, fixtures) {
  session.get(`/api/events/${pick(fixtures.pastEvents)}/ratings${query({ pageSize: 25 })}`, {
    name: 'GET /api/events/{eventId}/ratings',
  })
  const listed = body(
    session.get(`/api/events/${fixtures.rushEventId}/terms/decisions${query({ pageSize: 25 })}`, {
      name: 'GET /api/events/{eventId}/terms/decisions',
    }),
  ).items
  const signed = listed.filter((decision) => decision.hasSignature)
  if (signed.length) {
    const decision = pick(signed)
    session.get(
      `/api/events/${fixtures.rushEventId}/terms/decisions/${decision.userId}/${decision.termsDocumentId}/signature`,
      {
        name: 'GET /api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/signature',
        responseType: 'none',
      },
    )
  }
  session.get(`/api/files/${fixtures.thumbnailId}`, { name: 'GET /api/files/{fileId}' })
}

export function backoffice(data) {
  const { account, round } = nextAccount(data.admins.backoffice)
  const session = sessionFor(account)
  users(session)
  think()
  categories(session)
  documents(session)
  think()
  events(session, data.fixtures)
  featured(session, data.fixtures, round)
  signups(session, data, round)
  think()
  content(session, data.fixtures)
  think()
  legal(session, round)
  decisions(session, data.fixtures)
}
