import { body } from '../lib/http.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { downloadImage, uploadImage } from '../lib/fixtures.js'

let image = null

export function upload(data) {
  const { account } = nextAccount(data.admins.uploads)
  const session = sessionFor(account)
  if (!image) {
    image = downloadImage(session, data.fixtures.sourceFileId)
  }
  const file = body(uploadImage(session, image))
  if (!file) {
    return
  }
  session.get(`/api/files/${file.id}`, { name: 'GET /api/files/{fileId}' })
  session.get(`/api/files/${file.id}/content`, {
    name: 'GET /api/files/{fileId}/content',
    responseType: 'none',
  })
  session.get(`/api/files/${file.id}/thumbnail`, {
    name: 'GET /api/files/{fileId}/thumbnail',
    responseType: 'none',
  })
  session.delete(`/api/files/${file.id}`, { name: 'DELETE /api/files/{fileId}', expect: [204] })
}
