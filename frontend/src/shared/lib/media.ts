export function fileContentUrl(id?: string | null): string {
  return id ? `/api/files/${id}/content` : ''
}
