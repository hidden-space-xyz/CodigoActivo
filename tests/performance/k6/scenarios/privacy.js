import { ADMIN_EMAIL, PASSWORD } from '../lib/config.js'
import { body, query, think } from '../lib/http.js'
import { sessionFor } from '../lib/pool.js'
import { waitForCooldown } from '../lib/accounts.js'
import { LOGIN_CODE, waitForMail } from '../lib/mail.js'
import { pick } from '../lib/data.js'

const surnames = ['Lozano', 'Ibáñez', 'Gallego', 'Prieto', 'Robles', 'Herrera']
let consultation = null
let officer = null

function open(session) {
  waitForCooldown(officer)
  const since = Date.now()
  session.post('/api/blocked-data/consultations/code', {
    name: 'POST /api/blocked-data/consultations/code',
    json: { currentPassword: PASSWORD },
    expect: [204],
  })
  const [code] = waitForMail(ADMIN_EMAIL, LOGIN_CODE, { since })
  officer.codeAt = since
  const res = session.post('/api/blocked-data/consultations', {
    name: 'POST /api/blocked-data/consultations',
    json: {
      authority: 'Court',
      reference: `Diligencias ${since}`,
      currentPassword: PASSWORD,
      code,
    },
    expect: [201],
  })
  return body(res)
}

export function privacy(data) {
  officer = officer || { ...data.admin }
  const session = sessionFor(data.admin)
  const access = body(
    session.get('/api/blocked-data/access', { name: 'GET /api/blocked-data/access' }),
  )
  session.get(`/api/blocked-data/consultations${query({ pageSize: 25 })}`, {
    name: 'GET /api/blocked-data/consultations',
  })
  if (!consultation || Date.parse(consultation.expiresAt) - Date.now() < 120000) {
    consultation = open(session)
  }
  if (!consultation) {
    return
  }
  think()
  const found = body(
    session.post(`/api/blocked-data/consultations/${consultation.id}/searches`, {
      name: 'POST /api/blocked-data/consultations/{consultationId}/searches',
      json: { query: pick(surnames) },
    }),
  )
  const candidates = found?.candidates ?? []
  if (candidates.length) {
    session.post(`/api/blocked-data/consultations/${consultation.id}/extracts`, {
      name: 'POST /api/blocked-data/consultations/{consultationId}/extracts',
      json: { accountId: pick(candidates).accountId, entities: access?.entities ?? [] },
    })
  }
}
