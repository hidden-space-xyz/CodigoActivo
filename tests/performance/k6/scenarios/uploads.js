import { body } from '../lib/http.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { downloadImage, uploadImage } from '../lib/fixtures.js'
import { poster } from '../lib/pages.js'

let image = null

/** An editor picks a new image for a form: the upload, its preview and the generated thumbnail. */
export function uploads(data) {
  const { account } = nextAccount(data.admins.uploads)
  const session = sessionFor(account)
  image ??= downloadImage(session, data.fixtures.sourceFileId)
  if (!image) {
    return
  }
  const file = body(uploadImage(session, image))
  if (file) {
    poster(session, file.id)
    session.get('/api/files/{fileId}/thumbnail', {
      route: { fileId: file.id },
      responseType: 'none',
    })
  }
}
