const HEX_COLOR = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i

export function hexLuminance(hex: string): number {
  const red = parseInt(hex.slice(1, 3), 16)
  const green = parseInt(hex.slice(3, 5), 16)
  const blue = parseInt(hex.slice(5, 7), 16)
  return (0.299 * red + 0.587 * green + 0.114 * blue) / 255
}

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
