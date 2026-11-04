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

function eventsPage(session) {
  const upcoming = items(
    session.get('/api/events', {
      query: { scope: 'Upcoming', sort: 'eventStartsAt', page: 1, pageSize: 25 },
    }),
  )
  const years = body(session.get('/api/events/past-years')) ?? []
  session.get('/api/events/past-categories')
  const past = { scope: 'Past', year: years.length ? Math.max(...years) : undefined }
  session.get('/api/events', { query: { ...past, sort: '-eventStartsAt', page: 1, pageSize: 25 } })
  session.get('/api/events', {
    query: { ...past, search: word(), sort: '-eventStartsAt', page: 1, pageSize: 25 },
  })
  thumbnails(session, upcoming)
  return upcoming
}

function newsPage(session) {
  const years = body(session.get('/api/news/years')) ?? []
  const news = items(
    session.get('/api/news', {
      query: {
        year: years.length ? Math.max(...years) : undefined,
        sort: '-createdAt',
        page: 1,
        pageSize: 25,
      },
    }),
  )
  thumbnails(session, news)
  return news
}

function resourcesPage(session) {
  const resources = items(
    session.get('/api/resources', { query: { sort: '-createdAt', page: 1, pageSize: 25 } }),
  )
  thumbnails(session, resources)
  return resources
}

function detail(session, path, route) {
  const item = body(session.get(path, { route }))
  poster(session, item.thumbnailId)
}

export function visitors() {
  const session = new Session({ ip: clientIp('visitor', exec.scenario.iterationInTest) })
  openSite(session)
  home(session)
  think()

  const upcoming = eventsPage(session)
  think()

  if (upcoming.length) {
    const event = eventDetail(session, pick(upcoming).id)
    think()
    eventActivities(session, event)
    think()
  }

  const news = newsPage(session)
  think()
  if (news.length) {
    detail(session, '/api/news/{newsItemId}', { newsItemId: pick(news).id })
    think()
  }

  const resources = resourcesPage(session)
  think()
  if (resources.length) {
    detail(session, '/api/resources/{resourceId}', { resourceId: pick(resources).id })
    think()
  }
}
