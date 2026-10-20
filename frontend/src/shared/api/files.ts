/** Same-origin URL that streams a stored file's content; empty string when there is no file id. */
export function fileContentUrl(id?: string | null): string {
  return id ? `/api/files/${id}/content` : ''
}

/**
 * Same-origin URL that streams the smaller copy of a stored image that lists, cards and logos show;
 * empty string when there is no file id.
 */
export function fileThumbnailUrl(id?: string | null): string {
  return id ? `/api/files/${id}/thumbnail` : ''
}
