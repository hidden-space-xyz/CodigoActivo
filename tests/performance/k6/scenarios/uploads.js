import { body } from '../lib/http.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { downloadImage, uploadImage } from '../lib/fixtures.js'

let image = null

export function uploads(data) {
  const { account } = nextAccount(data.admins.uploads)
  const session = sessionFor(account)
  if (!image) {
    image = downloadImage(session, data.fixtures.sourceFileId)
  }
  const file = body(uploadImage(session, image))
  if (!file) {
    return
  }
  const route = { fileId: file.id }
  session.get('/api/files/{fileId}', { route })
  session.get('/api/files/{fileId}/content', { route, responseType: 'none' })
  session.get('/api/files/{fileId}/thumbnail', { route, responseType: 'none' })
  session.delete('/api/files/{fileId}', { route, expect: [204] })
}
