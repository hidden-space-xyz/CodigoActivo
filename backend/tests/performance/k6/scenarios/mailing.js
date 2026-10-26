import http from 'k6/http'
import encoding from 'k6/encoding'
import { query, think } from '../lib/http.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { CATALOG, pick, SIGNATURE_PNG, word } from '../lib/data.js'

const attachment = encoding.b64decode(SIGNATURE_PNG, 'std')

export function message(purpose, subject = `Novedades de ${word()}`) {
  return {
    subject,
    body: `Hola, os contamos las novedades del taller de ${word()}.`,
    purpose,
    attachments: http.file(attachment, 'cartel.png', 'image/png'),
  }
}

export function mailing(data) {
  const { account, iteration } = nextAccount(data.admins.mailing)
  const session = sessionFor(account)
  const { rushEventId } = data.fixtures
  const recipient = pick(data.members, iteration)

  session.post(`/api/emails/users/${recipient.id}`, {
    name: 'POST /api/emails/users/{userId}',
    form: message('Notice', 'Aviso sobre tu inscripción'),
  })
  think()

  const audience = { scope: 'Independent', newsConsent: true }
  session.get(`/api/emails/users/audience${query(audience)}`, {
    name: 'GET /api/emails/users/audience',
  })
  session.post(`/api/emails/users${query(audience)}`, {
    name: 'POST /api/emails/users',
    form: message('News'),
  })
  think()

  const attendees = { statusId: CATALOG.statuses.confirmed }
  session.get(`/api/emails/events/${rushEventId}/attendees/audience${query(attendees)}`, {
    name: 'GET /api/emails/events/{eventId}/attendees/audience',
  })
  session.post(`/api/emails/events/${rushEventId}/attendees${query(attendees)}`, {
    name: 'POST /api/emails/events/{eventId}/attendees',
    form: message('Notice', 'Recordatorio de la jornada'),
  })
}
