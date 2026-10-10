const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

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
