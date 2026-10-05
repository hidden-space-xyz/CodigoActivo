/**
 * Tells whether a link holds an ASCII control character. Browsers silently drop tabs and line
 * breaks from URLs, so a link that carries one may lead somewhere other than it reads (`/\t/host`
 * is another host); link checks refuse such values instead of reasoning about them.
 *
 * @param value - Link as typed or stored.
 * @returns `true` when the value holds a character below U+0020 or U+007F.
 */
export function hasControlCharacter(value: string): boolean {
  for (let index = 0; index < value.length; index += 1) {
    const code = value.charCodeAt(index)
    if (code < 0x20 || code === 0x7f) return true
  }
  return false
}

/**
 * Sanitizes a post-login `redirect` parameter before it reaches `router.push`. Only a path inside
 * this application is accepted: it must start with a single `/` and carry no backslash or control
 * character, which rules out protocol-relative (`//evil.test`) and `/\evil.test` targets that
 * browsers resolve to another origin.
 *
 * @param value - Raw query value, typically `route.query.redirect`.
 * @returns The value when it is a local path; otherwise `null`.
 */
export function toLocalRedirect(value: unknown): string | null {
  if (typeof value !== 'string') return null
  if (!value.startsWith('/') || value.startsWith('//')) return null
  if (value.includes('\\') || hasControlCharacter(value)) return null
  return value
}
