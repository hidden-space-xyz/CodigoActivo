const HEX_COLOR = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i

export function normalizeHexColor(input?: string | null): string | null {
  if (!input) return null
  const value = input.trim()
  if (!HEX_COLOR.test(value)) return null
  if (value.length === 4) {
    return `#${value
      .slice(1)
      .split('')
      .map((channel) => channel + channel)
      .join('')}`
  }
  return value
}
