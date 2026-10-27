import exec from 'k6/execution'
import { body, clientIp, parallel, query, Session, think } from '../lib/http.js'
import { pick, published, word } from '../lib/data.js'

const anonymous = (name) => `${name} [anonymous]`

function thumbnails(session, items) {
  parallel(
    items.slice(0, 6).map((item) => ({
      session,
      method: 'GET',
      path: `/api/files/${item.thumbnailId}/thumbnail`,
      options: { name: anonymous('GET /api/files/{fileId}/thumbnail'), responseType: 'none' },
    })),
  )
}

export function visitor() {
  const session = new Session({ ip: clientIp('visitor', exec.scenario.iterationInTest) })
  session.get('/api/auth/me', { name: anonymous('GET /api/auth/me'), expect: [401] })
  const upcoming = published(
    body(
      session.get(`/api/events${query({ scope: 'Upcoming', pageSize: 12 })}`, {
        name: anonymous('GET /api/events'),
      }),
    ).items,
  )
  session.get(`/api/events${query({ featured: true, pageSize: 1 })}`, {
    name: anonymous('GET /api/events'),
  })
  const news = published(
    body(session.get(`/api/news${query({ pageSize: 6 })}`, { name: anonymous('GET /api/news') }))
      .items,
  )
  const partners = published(
    body(
      session.get(`/api/partners${query({ pageSize: 50 })}`, {
        name: anonymous('GET /api/partners'),
      }),
    ).items,
  )
  thumbnails(session, [...upcoming, ...news])
  think()

  if (upcoming.length) {
    const event = pick(upcoming)
    session.get(`/api/events/${event.id}`, { name: anonymous('GET /api/events/{eventId}') })
    session.get(`/api/files/${event.thumbnailId}/content`, {
      name: anonymous('GET /api/files/{fileId}/content'),
      responseType: 'none',
    })
    session.get(`/api/files/${event.thumbnailId}`, { name: anonymous('GET /api/files/{fileId}') })
    const activities = body(
      session.get(`/api/activities${query({ eventId: event.id, pageSize: 50 })}`, {
        name: anonymous('GET /api/activities'),
      }),
    ).items
    thumbnails(session, activities)
    if (activities.length) {
      session.get(`/api/activities/${pick(activities).id}`, {
        name: anonymous('GET /api/activities/{activityId}'),
      })
    }
    think()
  }

  if (news.length) {
    session.get(`/api/news/${pick(news).id}`, { name: anonymous('GET /api/news/{newsItemId}') })
  }
  session.get('/api/news/years', { name: anonymous('GET /api/news/years') })
  think()

  const resources = published(
    body(
      session.get(`/api/resources${query({ pageSize: 12 })}`, {
        name: anonymous('GET /api/resources'),
      }),
    ).items,
  )
  if (resources.length) {
    session.get(`/api/resources/${pick(resources).id}`, {
      name: anonymous('GET /api/resources/{resourceId}'),
    })
  }
  if (partners.length) {
    session.get(`/api/partners/${pick(partners).id}`, {
      name: anonymous('GET /api/partners/{partnerId}'),
    })
  }
  think()

  const years = body(
    session.get('/api/events/past-years', { name: anonymous('GET /api/events/past-years') }),
  )
  const categories = body(
    session.get('/api/events/past-categories', {
      name: anonymous('GET /api/events/past-categories'),
    }),
  )
  session.get(
    `/api/events${query({
      scope: 'Past',
      year: years.length ? pick(years) : undefined,
      categoryTypeId: categories.length ? pick(categories).id : undefined,
      pageSize: 12,
    })}`,
    { name: anonymous('GET /api/events') },
  )
  session.get(`/api/events${query({ search: word(), scope: 'Past', pageSize: 12 })}`, {
    name: anonymous('GET /api/events'),
  })
  session.get(`/api/news${query({ search: word(), pageSize: 6 })}`, {
    name: anonymous('GET /api/news'),
  })
  session.get(`/api/resources${query({ search: word(), pageSize: 12 })}`, {
    name: anonymous('GET /api/resources'),
  })
  think()

  session.get('/api/legal-entity', { name: 'GET /api/legal-entity' })
  session.get(`/api/legal-policies/${pick(['Privacy', 'Cookies'])}`, {
    name: anonymous('GET /api/legal-policies/{kind}'),
  })
  session.get('/api/sitemap.xml', { name: 'GET /api/sitemap.xml' })
  session.get('/api/robots.txt', { name: 'GET /api/robots.txt' })
  session.get(`/api/${word()}-no-existe`, { name: 'GET /api/{unknown}', expect: [404] })
}
