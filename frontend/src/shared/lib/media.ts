/** Same-origin URL that streams a stored file's content; empty string when there is no file id. */
export function fileContentUrl(id?: string | null): string {
  return id ? `/api/files/${id}/content` : ''
}
