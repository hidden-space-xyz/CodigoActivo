import exec from 'k6/execution'
import { body, clientIp, Session } from '../lib/http.js'
import { think } from '../lib/profiles.js'
import { pick, word } from '../lib/data.js'
import {
  eventActivities,
  eventDetail,
  home,
  items,
  openSite,
  poster,
  thumbnails,
} from '../lib/pages.js'

const PAGE = { page: 1, pageSize: 25 }

function latest(years) {
  return years.length ? Math.max(...years) : undefined
}

function eventsPage(session) {
  const upcoming = items(
    session.get('/api/events', { query: { scope: 'Upcoming', sort: 'eventStartsAt', ...PAGE } }),
  )
  const years = body(session.get('/api/events/past-years')) ?? []
  session.get('/api/events/past-categories')
  const past = { scope: 'Past', year: latest(years), sort: '-eventStartsAt', ...PAGE }
  session.get('/api/events', { query: past })
  session.get('/api/events', { query: { ...past, search: word() } })
  thumbnails(session, upcoming)
  return upcoming
}

function newsPage(session) {
  const years = body(session.get('/api/news/years')) ?? []
  const news = items(
    session.get('/api/news', { query: { year: latest(years), sort: '-createdAt', ...PAGE } }),
  )
  thumbnails(session, news)
  return news
}

function resourcesPage(session) {
  const resources = items(session.get('/api/resources', { query: { sort: '-createdAt', ...PAGE } }))
  thumbnails(session, resources)
  return resources
}

function newsDetail(session, newsItemId) {
  const news = body(session.get('/api/news/{newsItemId}', { route: { newsItemId } }))
  poster(session, news?.thumbnailId)
}

function resourceDetail(session, resourceId) {
  const resource = body(session.get('/api/resources/{resourceId}', { route: { resourceId } }))
  if (!resource?.url) {
    poster(session, resource?.thumbnailId)
  }
}

/** An anonymous visit: home, events, an event and its activities, news and resources. */
export function visitors() {
  const session = new Session({ ip: clientIp('visitor', exec.scenario.iterationInTest) })
  openSite(session)
  home(session)
  think()

  const upcoming = eventsPage(session)
  think()

  const event = upcoming.length ? eventDetail(session, pick(upcoming).id) : null
  think()
  if (event) {
    eventActivities(session, event)
    think()
  }

  const news = newsPage(session)
  think()
  if (news.length) {
    newsDetail(session, pick(news).id)
    think()
  }

  const resources = resourcesPage(session)
  think()
  if (resources.length) {
    resourceDetail(session, pick(resources).id)
    think()
  }
}
