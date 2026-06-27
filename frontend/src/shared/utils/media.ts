import { env } from '@/shared/config/env'

/**
 * Public URL for a stored file's binary content. The backend serves
 * `GET /api/files/{id}/content` anonymously, so it can be used directly as an
 * `<img src>` on the public site.
 */
export function fileContentUrl(id?: string | null): string {
  return id ? `${env.apiBaseUrl}/api/files/${id}/content` : ''
}
