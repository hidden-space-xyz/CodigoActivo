import { body } from '../lib/http.js'
import { think } from '../lib/profiles.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import {
  activityBody,
  day,
  DRAFT,
  eventBody,
  LOOKUPS,
  pick,
  randomInt,
  richText,
  word,
} from '../lib/data.js'
import { MAX_PAGE_SIZE, poster } from '../lib/pages.js'

const PAGE = { page: 1, pageSize: 25 }
const INVENTORY_PAGE = { ...PAGE, sort: 'name' }

function usersPage(session) {
  const list = { scope: 'Independent', sort: 'firstName', ...PAGE }
  session.get('/api/users', { query: list })
  session.get('/api/users/types')
  session.get('/api/users/status-types')
  session.get('/api/users', { query: { ...list, name: pick(['a', 'e', 'o']) } })
}

function eventsPage(session) {
  session.get('/api/events', { query: { scope: 'Upcoming', sort: 'eventStartsAt', ...PAGE } })
  session.get('/api/events/categoryType', { query: { pageSize: MAX_PAGE_SIZE } })
  session.get('/api/events/termsDocument', { query: { pageSize: MAX_PAGE_SIZE } })
}

function eventPage(session, eventId) {
  const route = { eventId }
  session.get('/api/events/{eventId}', { route })
  session.get('/api/reports/events/{eventId}/summary', { route })
  session.get('/api/activities/modality-types')
  session.get('/api/activities/roleType')
  session.get('/api/activities', { query: { eventId, sort: 'activityStartsAt', ...PAGE } })
  session.get('/api/activities', {
    query: { eventId, sort: 'activityStartsAt', pageSize: MAX_PAGE_SIZE },
  })
  session.get('/api/activities/assignment-status-types')
  session.get('/api/users/types')
}

function attendeesTab(session, eventId) {
  session.get('/api/reports/events/{eventId}/attendees', {
    route: { eventId },
    query: { ...PAGE, sort: 'firstName' },
  })
}

function refreshAttendees(session, eventId) {
  attendeesTab(session, eventId)
  session.get('/api/reports/events/{eventId}/summary', { route: { eventId } })
}

function editDialog(session, path, route, thumbnailId) {
  session.get(path, { route })
  poster(session, thumbnailId)
}

function draftActivity(session, event, thumbnailId) {
  const plan = activityBody({
    thumbnailId,
    modalityId: LOOKUPS.modalities.online,
    roles: [{ activityRoleTypeId: LOOKUPS.roles.participant, desiredCount: 20 }],
    startsIn: 30,
  })
  const created = body(
    session.post('/api/activities/{eventId}', { route: event, json: plan, expect: [201] }),
  )
  if (!created) {
    return
  }
  const activity = { activityId: created.id }
  editDialog(session, '/api/activities/{activityId}', activity, thumbnailId)
  session.put('/api/activities/{activityId}', {
    route: activity,
    json: { ...plan, location: 'Online' },
  })
  think()
  session.delete('/api/activities/{activityId}', { route: activity, expect: [204] })
}

function draftEvent(session, fixtures, round) {
  const { thumbnailId } = fixtures
  const draft = eventBody({
    thumbnailId,
    categoryIds: [fixtures.categoryId],
    terms: [{ termsDocumentId: fixtures.conditionsId, required: true }],
    startsIn: 30,
  })
  const created = body(session.post('/api/events', { json: draft, expect: [201] }))
  if (!created) {
    return
  }
  const event = { eventId: created.id }
  eventPage(session, event.eventId)
  think()

  draftActivity(session, event, thumbnailId)
  editDialog(session, '/api/events/{eventId}', event, thumbnailId)
  session.put('/api/events/{eventId}', {
    route: event,
    json: { ...draft, subtitle: 'Evento revisado por la suite de rendimiento' },
  })
  session.patch('/api/events/{eventId}/feature', {
    route: { eventId: round % 2 ? fixtures.rushEventId : fixtures.featuredEventId },
  })
  think()

  session.delete('/api/events/{eventId}', { route: event, expect: [204] })
}

function signupDecisions(session, fixtures, round) {
  const { rushEventId: eventId, roster, pastEvents } = fixtures
  const [activityId] = fixtures.activities
  eventPage(session, eventId)
  attendeesTab(session, eventId)
  const member = pick(roster.slice(1), round)
  session.patch('/api/activities/{activityId}/{userId}/change-status', {
    route: { activityId, userId: member.id },
    json: {
      assignmentStatusId: round % 2 ? LOOKUPS.statuses.requested : LOOKUPS.statuses.confirmed,
    },
  })
  refreshAttendees(session, eventId)
  session.patch('/api/activities/{activityId}/{userId}/change-role', {
    route: { activityId, userId: member.dependents[0] },
    json: {
      activityRoleTypeId: round % 2 ? LOOKUPS.roles.volunteer : LOOKUPS.roles.participant,
    },
  })
  refreshAttendees(session, eventId)
  think()

  const decisions = body(
    session.get('/api/events/{eventId}/terms/decisions', {
      route: { eventId },
      query: { ...PAGE, sort: '-decidedAt' },
    }),
  )
  const signed = (decisions?.items ?? []).filter((decision) => decision.hasSignature)
  if (signed.length) {
    const { userId, termsDocumentId } = pick(signed)
    session.get('/api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/signature', {
      route: { eventId, userId, termsDocumentId },
      responseType: 'none',
    })
  }
  session.get('/api/events/{eventId}/ratings', {
    route: { eventId: pick(pastEvents) },
    query: { ...PAGE, sort: '-score' },
  })
}

function newsItem(session, fixtures, round) {
  const { thumbnailId } = fixtures
  session.get('/api/news', { query: { sort: '-createdAt', ...PAGE } })
  const draft = {
    title: `${DRAFT} noticia de ${word()} ${Date.now().toString(36)}`,
    subtitle: 'Noticia de la suite de rendimiento',
    description: richText(`Crónica del taller de ${word()}.`, thumbnailId),
    thumbnailId,
  }
  const created = body(session.post('/api/news', { json: draft, expect: [201] }))
  if (!created) {
    return
  }
  const news = { newsItemId: created.id }
  editDialog(session, '/api/news/{newsItemId}', news, thumbnailId)
  session.put('/api/news/{newsItemId}', {
    route: news,
    json: { ...draft, subtitle: 'Noticia revisada' },
  })
  session.patch('/api/news/{newsItemId}/feature', {
    route: { newsItemId: round % 2 ? fixtures.newsId : fixtures.featuredNewsId },
  })
  think()
  session.delete('/api/news/{newsItemId}', { route: news, expect: [204] })
}

function inventoryList(session) {
  return body(session.get('/api/inventory/categories', { query: INVENTORY_PAGE }))?.items ?? []
}

function inventoryCategoryWithEntries(session, route) {
  session.get('/api/inventory/categories/{categoryId}', { route })
  session.get('/api/inventory/categories/{categoryId}/entries', { route })
}

function draftInventoryCategory(session) {
  const draft = {
    name: `${DRAFT} ${word()} ${Date.now().toString(36)}${randomInt(0, 9999)}`,
    columns: [
      { id: null, name: 'Nombre', type: 'Text', required: true, options: [] },
      {
        id: null,
        name: 'Estado',
        type: 'List',
        required: false,
        options: [{ id: null, name: 'Disponible', color: '#16A34A' }],
      },
    ],
  }
  const created = body(session.post('/api/inventory/categories', { json: draft, expect: [201] }))
  inventoryList(session)
  if (!created) {
    return
  }
  const route = { categoryId: created.id }
  think()
  inventoryCategoryWithEntries(session, route)
  session.put('/api/inventory/categories/{categoryId}', {
    route,
    json: {
      name: created.name,
      columns: [
        ...created.columns.map(({ id, name, type, required, options }) => ({
          id,
          name,
          type,
          required,
          options,
        })),
        { id: null, name: 'Unidades', type: 'Quantity', required: false, options: [] },
      ],
    },
  })
  inventoryList(session)
  think()
  session.delete('/api/inventory/categories/{categoryId}', { route, expect: [204] })
  inventoryList(session)
}

function entryValues(columns) {
  const byType = {
    Text: () => `${DRAFT} ${word()}`,
    LongText: () => `Revisión de ${word()} antes del taller.`,
    Number: () => '12.5',
    Date: () => day(0),
    Boolean: () => 'true',
    List: (column) => column.options[0]?.id,
  }
  return Object.fromEntries(
    columns
      .filter(({ type }) => byType[type])
      .map((column) => [column.id, byType[column.type](column)]),
  )
}

function inventoryCategory(session, categoryId) {
  const route = { categoryId }
  const category = body(session.get('/api/inventory/categories/{categoryId}', { route }))
  session.get('/api/inventory/categories/{categoryId}/entries', { route })
  if (!category) {
    return
  }
  think()
  session.get('/api/inventory/categories/{categoryId}/entries', { route })
  const counted = category.columns.some(({ type }) => type === 'Quantity')
  const created = body(
    session.post('/api/inventory/categories/{categoryId}/entries', {
      route,
      json: { quantity: counted ? 1 : null, values: entryValues(category.columns) },
      expect: [201],
    }),
  )
  inventoryCategoryWithEntries(session, route)
  if (!created) {
    return
  }
  const entry = { ...route, entryId: created.id }
  think()
  if (counted) {
    session.put('/api/inventory/categories/{categoryId}/entries/{entryId}', {
      route: entry,
      json: { quantity: created.quantity + 1, values: created.values },
    })
    inventoryCategoryWithEntries(session, route)
  }
  think()
  session.delete('/api/inventory/categories/{categoryId}/entries/{entryId}', {
    route: entry,
    expect: [204],
  })
  inventoryCategoryWithEntries(session, route)
}

function inventory(session) {
  const categories = inventoryList(session)
  think()
  draftInventoryCategory(session)
  think()
  const [largest] = [...categories].sort((a, b) => b.entryCount - a.entryCount)
  if (largest) {
    inventoryCategory(session, largest.id)
  }
}

/**
 * An administrator reviews users, runs a draft event to its deletion, decides signups, edits news
 * and keeps the inventory.
 */
export function backoffice(data) {
  const { account, round } = nextAccount(data.admins.backoffice)
  const session = sessionFor(account)
  usersPage(session)
  think()
  eventsPage(session)
  think()
  draftEvent(session, data.fixtures, round)
  think()
  signupDecisions(session, data.fixtures, round)
  think()
  newsItem(session, data.fixtures, round)
  think()
  inventory(session)
  think()
}
