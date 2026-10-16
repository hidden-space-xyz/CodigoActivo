import { hasControlCharacter } from './safe-redirect'

/**
 * The trimmed value when it is an absolute http(s) address with a host, without credentials or
 * control characters: the links the API accepts for partners and resources (`[HttpUrl]`).
 * Forms use it to refuse what the API would, and mappers to keep anything else, such as a
 * `javascript:` value, out of an `href`.
 *
 * @param value - Link as typed or received.
 * @returns The trimmed link, or `null` when it is not such an address.
 */
export function toWebUrl(value: string | null | undefined): string | null {
  const trimmed = value?.trim()
  if (!trimmed || hasControlCharacter(trimmed)) return null
  try {
    const parsed = new URL(trimmed)
    const web = parsed.protocol === 'http:' || parsed.protocol === 'https:'
    return web && parsed.hostname && !parsed.username && !parsed.password ? trimmed : null
  } catch {
    return null
  }
}

const BIDI_CONTROLS = /[‎‏‪-‮⁦-⁩]/g

function decodeForDisplay(value: string): string {
  try {
    return decodeURI(value).replace(BIDI_CONTROLS, '')
  } catch {
    return value
  }
}

/**
 * Host and path of a web address without scheme, "www." or the path's trailing slash, for compact
 * display. The host is taken as written, so internationalized domains stay readable instead of
 * showing as punycode; the input is returned unchanged when it does not parse.
 */
export function displayUrl(url: string): string {
  let parsed: URL
  try {
    parsed = new URL(url)
  } catch {
    return url
  }
  const [host = ''] = url
    .trim()
    .replace(/^[a-z][a-z\d+.-]*:\/\//i, '')
    .split(/[/?#]/, 1)
  const path = parsed.search || parsed.hash ? parsed.pathname : parsed.pathname.replace(/\/$/, '')
  return `${host.toLowerCase().replace(/^www\./, '')}${decodeForDisplay(`${path}${parsed.search}${parsed.hash}`)}`
}
