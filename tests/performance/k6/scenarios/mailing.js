import http from 'k6/http'
import encoding from 'k6/encoding'
import { think } from '../lib/profiles.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { LOOKUPS, pick, SIGNATURE_PNG, word } from '../lib/data.js'

const attachment = encoding.b64decode(SIGNATURE_PNG, 'std')

function message(purpose, subject) {
  return {
    subject,
    body: `Hola, os contamos las novedades del taller de ${word()}.`,
    purpose,
    attachments: http.file(attachment, 'cartel.png', 'image/png'),
  }
}

/** An administrator emails one member, the subscribers and the attendees of an event. */
export function mailing(data) {
  const { account, iteration } = nextAccount(data.admins.mailing)
  const session = sessionFor(account)
  const eventId = data.fixtures.rushEventId
  const { id: userId } = pick(data.members, iteration)

  session.get('/api/emails/users/audience', { query: { id: userId } })
  session.post('/api/emails/users/{userId}', {
    route: { userId },
    form: message('Notice', 'Aviso sobre tu inscripción'),
  })
  think()

  const audience = { scope: 'Independent', newsConsent: true }
  session.get('/api/emails/users/audience', { query: audience })
  session.post('/api/emails/users', {
    query: audience,
    form: message('News', `Novedades de ${word()}`),
  })
  think()

  const attendees = { statusId: LOOKUPS.statuses.confirmed }
  session.get('/api/emails/events/{eventId}/attendees/audience', {
    route: { eventId },
    query: attendees,
  })
  session.post('/api/emails/events/{eventId}/attendees', {
    route: { eventId },
    query: attendees,
    form: message('Notice', 'Recordatorio de la jornada'),
  })
}
