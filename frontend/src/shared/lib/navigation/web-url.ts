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
