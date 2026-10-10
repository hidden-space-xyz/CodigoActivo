/**
 * Route param pattern that only matches a GUID. Detail routes declare their id with it because the
 * param becomes a path segment of API requests, and Vue Router decodes `%2F`, `%3F` and `%23` in
 * params: any other value could point the request, with the visitor's session, at another endpoint.
 */
export const GUID_PARAM =
  '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}'

const GUID_PATTERN = new RegExp(`^${GUID_PARAM}$`)

/**
 * Tells whether a value read from a URL is a GUID. Identifiers taken from a link become path
 * segments of API requests, so only this shape may pass: a crafted value holding `../` or `?` would
 * otherwise point the request, with the visitor's session, at another endpoint.
 *
 * @param value - Raw value, typically a parameter of an emailed link.
 * @returns `true` for a GUID in any letter case.
 */
export function isGuid(value: string | null | undefined): value is string {
  return typeof value === 'string' && GUID_PATTERN.test(value)
}
