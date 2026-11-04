import { body, parallel } from './http.js'
import { published } from './data.js'

const VISIBLE_CARDS = 6
export const MAX_PAGE_SIZE = 100

export function items(res) {
  return published(body(res)?.items ?? [])
}

export function thumbnails(session, cards) {
  parallel(
    cards
      .filter(({ thumbnailId }) => thumbnailId)
      .slice(0, VISIBLE_CARDS)
      .map(({ thumbnailId }) => ({
        session,
        method: 'GET',
        path: '/api/files/{fileId}/thumbnail',
        options: { route: { fileId: thumbnailId }, responseType: 'none' },
      })),
  )
}

export function poster(session, fileId) {
  if (fileId) {
    session.get('/api/files/{fileId}/content', { route: { fileId }, responseType: 'none' })
  }
}

export function openSite(session) {
  session.get('/api/auth/me', { expect: session.user ? [200] : [401] })
  session.get('/api/legal-entity')
}

export function home(session) {
  const news = items(
    session.get('/api/news', { query: { sort: '-featured,-createdAt', pageSize: 4 } }),
  )
  const [featured] = items(
    session.get('/api/events', { query: { featured: true, scope: 'Upcoming', pageSize: 1 } }),
  )
  const upcoming = items(
    session.get('/api/events', {
      query: { scope: 'Upcoming', sort: 'eventStartsAt', pageSize: 4 },
    }),
  )
  const partners = items(
    session.get('/api/partners', { query: { sort: 'tier,-fromDate', pageSize: MAX_PAGE_SIZE } }),
  )
  if (featured) {
    poster(session, featured.thumbnailId)
  }
  thumbnails(session, [...news, ...upcoming, ...partners])
  return upcoming
}

export function household(session) {
  return items(
    session.get('/api/users', {
      query: { parentId: session.userId, sort: 'firstName', pageSize: MAX_PAGE_SIZE },
    }),
  )
}

export function accountProfile(session) {
  session.get('/api/auth/me')
  household(session)
  session.get('/api/me/consents')
}

export function eventDetail(session, eventId) {
  const event = body(session.get('/api/events/{eventId}', { route: { eventId } }))
  if (session.user) {
    session.get('/api/events/{eventId}/leader-roster', { route: { eventId } })
  }
  poster(session, event.thumbnailId)
  return event
}

export function signupState(session, eventId) {
  session.get('/api/activities', {
    query: { eventId, sort: 'activityStartsAt', pageSize: MAX_PAGE_SIZE },
  })
  if (session.user) {
    session.get('/api/me/assigned-activities', { query: { eventId } })
    session.get('/api/activities/household-assignments/{eventId}', { route: { eventId } })
  }
}

export function eventTerms(session, event) {
  return event.termsDocuments.length
    ? body(session.get('/api/events/{eventId}/terms', { route: { eventId: event.id } }))
    : null
}

export function eventActivities(session, event) {
  signupState(session, event.id)
  if (!session.user) {
    return null
  }
  household(session)
  session.get('/api/activities/signup-roles')
  return eventTerms(session, event)
}
