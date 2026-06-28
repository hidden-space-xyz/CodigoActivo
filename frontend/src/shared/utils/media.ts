import { env } from '@/shared/config/env'

export function fileContentUrl(id?: string | null): string {
  return id ? `${env.apiBaseUrl}/api/files/${id}/content` : ''
}
